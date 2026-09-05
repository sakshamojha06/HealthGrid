using HealthGrid.Api.Contracts;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(AnalyticsService analytics) : ControllerBase
{
    /// <summary>Daily recorded-visit counts across a bounded date range (scoped).</summary>
    [HttpGet("patient-volume")]
    public Task<IReadOnlyList<TimeSeriesPoint>> PatientVolume(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => analytics.PatientVolumeAsync(from, to, ct);

    /// <summary>Per-disease daily case counts across a bounded date range (scoped).</summary>
    [HttpGet("diseases")]
    public Task<IReadOnlyList<DiseaseTrendDto>> Diseases(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct)
        => analytics.DiseaseTrendsAsync(from, to, ct);
}
