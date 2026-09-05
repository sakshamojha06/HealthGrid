using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

public sealed class VisitError(string message) : Exception(message);

/// <summary>
/// Records patient visits for the caller's PHC. Recording a visit also issues the
/// prescribed medicines from that PHC's stock through <see cref="InventoryService"/>.
/// </summary>
public sealed class VisitService(
    HealthGridDbContext db,
    ICurrentUser currentUser,
    AnalyticsService analytics,
    InventoryService inventory,
    AuditWriter audit)
{
    public async Task<PatientVisitDto> RecordAsync(RecordVisitRequest request, CancellationToken ct)
    {
        if (currentUser.PhcId is not { } phcId || currentUser.DistrictId is not { } districtId)
            throw new VisitError("Only a PHC-assigned user can record a visit.");
        currentUser.EnsureAccess(districtId, phcId);

        if (string.IsNullOrWhiteSpace(request.LocalIdentifier))
            throw new VisitError("A patient identifier is required.");
        if (request.DiseaseIds is not { Count: > 0 })
            throw new VisitError("At least one diagnosis is required.");
        if (request.VisitDate > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1))
            throw new VisitError("Visit date cannot be in the future.");

        var diseaseIds = request.DiseaseIds.Distinct().ToList();
        var knownDiseases = await db.Diseases.CountAsync(d => diseaseIds.Contains(d.Id), ct);
        if (knownDiseases != diseaseIds.Count)
            throw new VisitError("One or more diagnoses are not recognised.");

        var items = (request.PrescriptionItems ?? [])
            .Where(i => i.Quantity > 0)
            .GroupBy(i => i.MedicineId)
            .Select(g => new PrescriptionItemInput(g.Key, g.Sum(x => x.Quantity)))
            .ToList();

        if (request.AttendingDoctorId is { } docId)
        {
            var ok = await db.Doctors.AnyAsync(d => d.Id == docId && d.PhcId == phcId, ct);
            if (!ok)
                throw new VisitError("The attending doctor is not registered at this PHC.");
        }

        var patient = await db.Patients.SingleOrDefaultAsync(
            p => p.DistrictId == districtId && p.PhcId == phcId
                 && p.LocalIdentifier == request.LocalIdentifier, ct);
        if (patient is null)
        {
            patient = new Patient
            {
                DistrictId = districtId,
                PhcId = phcId,
                LocalIdentifier = request.LocalIdentifier.Trim(),
                BirthYear = request.BirthYear,
                Gender = Normalise(request.Gender),
            };
            db.Patients.Add(patient);
        }
        else
        {
            patient.BirthYear ??= request.BirthYear;
            patient.Gender ??= Normalise(request.Gender);
        }

        var visit = new PatientVisit
        {
            DistrictId = districtId,
            PhcId = phcId,
            Patient = patient,
            AttendingDoctorId = request.AttendingDoctorId,
            VisitDate = request.VisitDate,
            Symptoms = string.IsNullOrWhiteSpace(request.Symptoms) ? null : request.Symptoms.Trim(),
            Diagnoses = diseaseIds.Select(id => new VisitDiagnosis
            {
                DistrictId = districtId,
                PhcId = phcId,
                DiseaseId = id,
            }).ToList(),
            PrescriptionItems = items.Select(i => new VisitPrescriptionItem
            {
                DistrictId = districtId,
                PhcId = phcId,
                MedicineId = i.MedicineId,
                Quantity = i.Quantity,
            }).ToList(),
        };
        db.PatientVisits.Add(visit);
        await db.SaveChangesAsync(ct);

        // Issue prescribed stock. Best-effort: a visit is still recorded even if a
        // medicine is short — the shortfall surfaces as a negative-stock error to the
        // caller only when it would break the ledger.
        foreach (var item in items)
        {
            await inventory.ApplyAsync(phcId, item.MedicineId, -item.Quantity,
                InventoryTransactionType.PatientIssue, $"Visit {visit.Id}",
                $"visit-{visit.Id}-{item.MedicineId}", ct);
        }

        await audit.WriteAsync("PatientVisitRecorded", nameof(PatientVisit), visit.Id, ct: ct);
        return await ProjectAsync(visit.Id, ct);
    }

    public async Task<PagedResult<PatientVisitDto>> ListAsync(
        int page, int pageSize, DateOnly? fromDate, DateOnly? toDate,
        Guid? diseaseId, string? search, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = analytics.ScopedVisits().Include(v => v.Patient).AsQueryable();
        if (fromDate is { } f)
            query = query.Where(v => v.VisitDate >= f);
        if (toDate is { } t)
            query = query.Where(v => v.VisitDate <= t);
        if (diseaseId is { } d)
            query = query.Where(v => v.Diagnoses.Any(x => x.DiseaseId == d));
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(v => v.Patient!.LocalIdentifier.Contains(search));

        var total = await query.CountAsync(ct);
        var ids = await query
            .OrderByDescending(v => v.VisitDate).ThenByDescending(v => v.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => v.Id).ToListAsync(ct);

        var items = new List<PatientVisitDto>(ids.Count);
        foreach (var id in ids)
            items.Add(await ProjectAsync(id, ct));

        return new PagedResult<PatientVisitDto>(items, page, pageSize, total);
    }

    public async Task<PatientVisitDto> GetAsync(Guid id, CancellationToken ct)
    {
        var visit = await analytics.ScopedVisits().FirstOrDefaultAsync(v => v.Id == id, ct)
                    ?? throw new NotFoundError("Visit not found.");
        return await ProjectAsync(visit.Id, ct);
    }

    private async Task<PatientVisitDto> ProjectAsync(Guid id, CancellationToken ct)
    {
        var v = await db.PatientVisits.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.AttendingDoctor)
            .Include(x => x.Diagnoses).ThenInclude(d => d.Disease)
            .Include(x => x.PrescriptionItems).ThenInclude(p => p.Medicine)
            .SingleAsync(x => x.Id == id, ct);

        var phcName = await db.Phcs.AsNoTracking().Where(p => p.Id == v.PhcId).Select(p => p.Name).SingleAsync(ct);

        return new PatientVisitDto(
            v.Id, v.PhcId, phcName, v.Patient!.LocalIdentifier, v.Patient.BirthYear, v.Patient.Gender,
            v.VisitDate, v.Symptoms, v.AttendingDoctor?.Name,
            v.Diagnoses.Select(d => d.Disease!.Name).ToList(),
            v.PrescriptionItems.Select(p => new PrescribedMedicineDto(p.Medicine!.Name, p.Quantity)).ToList());
    }

    private static string? Normalise(string? gender) => gender?.Trim().ToUpperInvariant() switch
    {
        "M" or "F" or "O" => gender!.Trim().ToUpperInvariant(),
        _ => null,
    };
}
