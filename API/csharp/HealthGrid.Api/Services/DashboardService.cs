using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

/// <summary>One scoped aggregation for the operational dashboard (plan section 10).</summary>
public sealed class DashboardService(
    HealthGridDbContext db,
    ICurrentUser currentUser,
    AnalyticsService analytics,
    InventoryService inventory,
    PredictionService predictions)
{
    public async Task<DashboardSummaryDto> BuildAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = today.AddDays(-29);

        var volumeTrend = await analytics.PatientVolumeAsync(from, today, ct);
        var diseaseTrends = await analytics.DiseaseTrendsAsync(from, today, ct);
        var todayCount = (int)(volumeTrend.LastOrDefault()?.Value ?? 0);

        var stock = await inventory.GetStockAsync(null, null, lowOnly: false, ct);
        var lowStock = stock.Where(s => s.IsLow).OrderBy(s => s.QuantityOnHand).ToList();

        var snapshot = await predictions.LatestSnapshotAsync(ct);
        var forecast = snapshot?.Values
            .Where(v => v.Kind == "PatientVolume")
            .OrderBy(v => v.TargetDate)
            .Select(v => new ForecastPoint(v.TargetDate, v.PointForecast, v.LowerBound, v.UpperBound))
            .ToList() ?? [];

        var medNames = await db.Medicines.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Name, ct);
        var phcNames = await db.Phcs.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var stockoutRisks = snapshot?.StockoutRisks.Select(r => new StockoutRiskDto(
            r.MedicineId, medNames.GetValueOrDefault(r.MedicineId, "?"),
            phcNames.GetValueOrDefault(r.PhcId, "?"),
            r.DaysToStockout, r.StockoutDate, r.Risk.ToString(), r.RecommendedReorderQty)).ToList() ?? [];

        var aiAlerts = await ScopedAlerts()
            .OrderByDescending(a => a.CreatedAtUtc).Take(20)
            .Select(a => new AiAlertDto(a.Id, a.AlertType, a.Message, a.Severity.ToString(),
                a.IsAcknowledged, a.CreatedAtUtc))
            .ToListAsync(ct);

        var myPhc = currentUser.PhcId;
        var requests = db.MedicineRequests.AsNoTracking();
        if (!currentUser.IsSystemAdministrator)
            requests = requests.Where(r => r.DistrictId == currentUser.DistrictId);

        var pendingIncoming = await requests.CountAsync(
            r => r.DestinationPhcId == myPhc && r.Status == MedicineRequestStatus.Pending, ct);
        var pendingOutgoing = await requests.CountAsync(
            r => r.SourcePhcId == myPhc
                 && r.Status != MedicineRequestStatus.Completed
                 && r.Status != MedicineRequestStatus.Rejected, ct);

        var specialists = await ScopedDoctors()
            .Where(d => d.SpecializationId != null)
            .GroupBy(d => d.Specialization!.Name)
            .Select(g => new SpecialistCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        return new DashboardSummaryDto(
            GeneratedAtUtc: DateTime.UtcNow,
            PredictionStatus: snapshot?.Status ?? "InsufficientData",
            PredictionModelVersion: snapshot?.ModelVersion,
            TodayPatientCount: todayCount,
            PatientVolumeTrend: volumeTrend,
            PatientVolumeForecast: forecast,
            PatientVolumePctChange: snapshot?.PatientVolumePctChange,
            DiseaseTrends: diseaseTrends.Take(6).ToList(),
            InventorySummary: new InventorySummaryDto(stock.Count, lowStock.Count),
            LowStock: lowStock.Take(10).ToList(),
            StockoutRisks: stockoutRisks,
            AiAlerts: aiAlerts,
            PendingIncomingRequests: pendingIncoming,
            PendingOutgoingRequests: pendingOutgoing,
            AvailableSpecialists: specialists);
    }

    private IQueryable<AiAlert> ScopedAlerts()
    {
        var q = db.AiAlerts.AsNoTracking();
        if (currentUser.IsSystemAdministrator)
            return q;
        q = q.Where(a => a.DistrictId == currentUser.DistrictId);
        if (currentUser.PhcId is { } phc && !currentUser.IsInRole(Roles.DistrictAdministrator))
            q = q.Where(a => a.PhcId == null || a.PhcId == phc);
        return q;
    }

    private IQueryable<Doctor> ScopedDoctors()
    {
        var q = db.Doctors.AsNoTracking().Include(d => d.Specialization).AsQueryable();
        if (currentUser.IsSystemAdministrator)
            return q;
        return q.Where(d => d.DistrictId == currentUser.DistrictId);
    }
}
