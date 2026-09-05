using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

/// <summary>
/// Scoped aggregate analytics. Every query is filtered to the caller's district
/// (and PHC for PHC-bound roles) — district administrators see aggregates only,
/// never individual patient rows.
/// </summary>
public sealed class AnalyticsService(HealthGridDbContext db, ICurrentUser currentUser)
{
    private const int MaxRangeDays = 366;

    public async Task<IReadOnlyList<TimeSeriesPoint>> PatientVolumeAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        (from, to) = Clamp(from, to);

        var counts = await ScopedVisits()
            .Where(v => v.VisitDate >= from && v.VisitDate <= to)
            .GroupBy(v => v.VisitDate)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byDate = counts.ToDictionary(x => x.Date, x => x.Count);
        return EachDay(from, to)
            .Select(d => new TimeSeriesPoint(d, byDate.GetValueOrDefault(d, 0)))
            .ToList();
    }

    public async Task<IReadOnlyList<DiseaseTrendDto>> DiseaseTrendsAsync(
        DateOnly from, DateOnly to, CancellationToken ct)
    {
        (from, to) = Clamp(from, to);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await ScopedDiagnoses()
            .Where(d => d.PatientVisit!.VisitDate >= from && d.PatientVisit.VisitDate <= to)
            .Select(d => new { d.DiseaseId, DiseaseName = d.Disease!.Name, Date = d.PatientVisit!.VisitDate })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => new { r.DiseaseId, r.DiseaseName })
            .Select(g =>
            {
                var byDate = g.GroupBy(x => x.Date).ToDictionary(x => x.Key, x => x.Count());
                var series = EachDay(from, to)
                    .Select(d => new TimeSeriesPoint(d, byDate.GetValueOrDefault(d, 0)))
                    .ToList();
                return new DiseaseTrendDto(
                    g.Key.DiseaseId, g.Key.DiseaseName,
                    byDate.GetValueOrDefault(today, 0), series);
            })
            .OrderByDescending(t => t.Series.Sum(p => p.Value))
            .ToList();
    }

    public IQueryable<PatientVisit> ScopedVisits()
    {
        var q = db.PatientVisits.AsNoTracking();
        if (currentUser.IsSystemAdministrator)
            return q;
        q = q.Where(v => v.DistrictId == currentUser.DistrictId);
        if (currentUser.PhcId is { } phc && !currentUser.IsInRole(Roles.DistrictAdministrator))
            q = q.Where(v => v.PhcId == phc);
        return q;
    }

    public IQueryable<VisitDiagnosis> ScopedDiagnoses()
    {
        var q = db.VisitDiagnoses.AsNoTracking().Include(d => d.PatientVisit).Include(d => d.Disease);
        if (currentUser.IsSystemAdministrator)
            return q;
        var filtered = q.Where(d => d.DistrictId == currentUser.DistrictId);
        if (currentUser.PhcId is { } phc && !currentUser.IsInRole(Roles.DistrictAdministrator))
            filtered = filtered.Where(d => d.PhcId == phc);
        return filtered;
    }

    private static (DateOnly From, DateOnly To) Clamp(DateOnly from, DateOnly to)
    {
        if (to < from)
            (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > MaxRangeDays)
            from = to.AddDays(-MaxRangeDays);
        return (from, to);
    }

    private static IEnumerable<DateOnly> EachDay(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
            yield return d;
    }
}
