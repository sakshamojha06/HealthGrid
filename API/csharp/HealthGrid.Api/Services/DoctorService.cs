using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

public sealed class DoctorError(string message) : Exception(message);

/// <summary>Doctor / specialist directory, scoped to the caller's district.</summary>
public sealed class DoctorService(HealthGridDbContext db, ICurrentUser currentUser, AuditWriter audit)
{
    public async Task<IReadOnlyList<DoctorDto>> SearchAsync(
        Guid? specializationId, Guid? phcId, int? day, CancellationToken ct)
    {
        var query = Scoped();
        if (specializationId is { } s)
            query = query.Where(d => d.SpecializationId == s);
        if (phcId is { } p)
            query = query.Where(d => d.PhcId == p);
        if (day is { } dow)
            query = query.Where(d => d.Availability.Any(a => a.DayOfWeek == dow));

        var ids = await query.OrderBy(d => d.Name).Select(d => d.Id).ToListAsync(ct);
        var list = new List<DoctorDto>(ids.Count);
        foreach (var id in ids)
            list.Add(await ProjectAsync(id, ct));
        return list;
    }

    public async Task<DoctorDto> GetAsync(Guid id, CancellationToken ct)
    {
        _ = await Scoped().FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundError("Doctor not found.");
        return await ProjectAsync(id, ct);
    }

    public async Task<DoctorDto> CreateAsync(DoctorInput input, CancellationToken ct)
    {
        var (districtId, _) = await ResolvePhcAsync(input.PhcId, ct);
        currentUser.EnsureAccess(districtId, input.PhcId);
        await EnsureSpecializationAsync(input.SpecializationId, ct);

        var doctor = new Doctor
        {
            DistrictId = districtId,
            PhcId = input.PhcId,
            Name = input.Name.Trim(),
            RegistrationNumber = input.RegistrationNumber?.Trim(),
            SpecializationId = input.SpecializationId,
        };
        db.Doctors.Add(doctor);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DoctorCreated", nameof(Doctor), doctor.Id, ct: ct);
        return await ProjectAsync(doctor.Id, ct);
    }

    public async Task<DoctorDto> UpdateAsync(Guid id, DoctorInput input, CancellationToken ct)
    {
        var doctor = await Scoped().FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundError("Doctor not found.");
        var tracked = await db.Doctors.SingleAsync(d => d.Id == id, ct);

        var (districtId, _) = await ResolvePhcAsync(input.PhcId, ct);
        currentUser.EnsureAccess(districtId, input.PhcId);
        await EnsureSpecializationAsync(input.SpecializationId, ct);

        tracked.Name = input.Name.Trim();
        tracked.PhcId = input.PhcId;
        tracked.DistrictId = districtId;
        tracked.RegistrationNumber = input.RegistrationNumber?.Trim();
        tracked.SpecializationId = input.SpecializationId;
        tracked.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DoctorUpdated", nameof(Doctor), id, ct: ct);
        return await ProjectAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var doctor = await Scoped().FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundError("Doctor not found.");
        var inUse = await db.PatientVisits.AnyAsync(v => v.AttendingDoctorId == id, ct);
        if (inUse)
            throw new DoctorError("This doctor is linked to recorded visits and cannot be deleted.");

        db.Doctors.Remove(await db.Doctors.SingleAsync(d => d.Id == id, ct));
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DoctorDeleted", nameof(Doctor), id, ct: ct);
    }

    public async Task<DoctorDto> SetAvailabilityAsync(
        Guid id, SetAvailabilityInput input, CancellationToken ct)
    {
        var doctor = await Scoped().FirstOrDefaultAsync(d => d.Id == id, ct)
                     ?? throw new NotFoundError("Doctor not found.");
        currentUser.EnsureAccess(doctor.DistrictId, doctor.PhcId);

        var existing = db.DoctorAvailabilities.Where(a => a.DoctorId == id);
        db.DoctorAvailabilities.RemoveRange(existing);

        foreach (var slot in input.Slots ?? [])
        {
            if (!TimeOnly.TryParse(slot.StartTime, out var start) || !TimeOnly.TryParse(slot.EndTime, out var end))
                throw new DoctorError("Availability times must be in HH:mm format.");
            if (end <= start)
                throw new DoctorError("Availability end time must be after the start time.");

            db.DoctorAvailabilities.Add(new DoctorAvailability
            {
                DistrictId = doctor.DistrictId,
                PhcId = doctor.PhcId,
                DoctorId = id,
                DayOfWeek = slot.DayOfWeek,
                StartTime = start,
                EndTime = end,
            });
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DoctorAvailabilitySet", nameof(Doctor), id, ct: ct);
        return await ProjectAsync(id, ct);
    }

    private IQueryable<Doctor> Scoped()
    {
        var q = db.Doctors.AsNoTracking();
        if (currentUser.IsSystemAdministrator)
            return q;
        return q.Where(d => d.DistrictId == currentUser.DistrictId);
    }

    private async Task<(Guid DistrictId, string Name)> ResolvePhcAsync(Guid phcId, CancellationToken ct)
    {
        var phc = await db.Phcs.AsNoTracking().SingleOrDefaultAsync(p => p.Id == phcId, ct)
                  ?? throw new DoctorError("Unknown PHC.");
        return (phc.DistrictId, phc.Name);
    }

    private async Task EnsureSpecializationAsync(Guid? specializationId, CancellationToken ct)
    {
        if (specializationId is { } id && !await db.Specializations.AnyAsync(s => s.Id == id, ct))
            throw new DoctorError("Unknown specialization.");
    }

    private async Task<DoctorDto> ProjectAsync(Guid id, CancellationToken ct)
    {
        var d = await db.Doctors.AsNoTracking()
            .Include(x => x.Specialization)
            .Include(x => x.Availability)
            .SingleAsync(x => x.Id == id, ct);
        var phcName = await db.Phcs.AsNoTracking().Where(p => p.Id == d.PhcId).Select(p => p.Name).SingleAsync(ct);

        return new DoctorDto(
            d.Id, d.PhcId, phcName, d.Name, d.RegistrationNumber,
            d.SpecializationId, d.Specialization?.Name,
            d.Availability.OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
                .Select(a => new DoctorAvailabilityDto(
                    a.Id, a.DayOfWeek, a.StartTime.ToString("HH:mm"), a.EndTime.ToString("HH:mm")))
                .ToList());
    }
}
