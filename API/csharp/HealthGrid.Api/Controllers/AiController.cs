using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(
    HealthGridDbContext db,
    ICurrentUser currentUser,
    PredictionService predictions,
    AuditWriter audit) : ControllerBase
{
    /// <summary>Latest versioned forecast + stock-out risks for the caller's scope.</summary>
    [HttpGet("predictions")]
    public Task<PredictionsResponse> Predictions(CancellationToken ct) => predictions.GetPredictionsAsync(ct);

    /// <summary>Open AI alerts in scope, newest first.</summary>
    [HttpGet("alerts")]
    public async Task<IReadOnlyList<AiAlertDto>> Alerts(CancellationToken ct)
    {
        var query = db.AiAlerts.AsNoTracking();
        if (!currentUser.IsSystemAdministrator)
        {
            query = query.Where(a => a.DistrictId == currentUser.DistrictId);
            if (currentUser.PhcId is { } phc && !currentUser.IsInRole(Roles.DistrictAdministrator))
                query = query.Where(a => a.PhcId == null || a.PhcId == phc);
        }

        return await query.OrderByDescending(a => a.CreatedAtUtc).Take(100)
            .Select(a => new AiAlertDto(
                a.Id, a.AlertType, a.Message, a.Severity.ToString(), a.IsAcknowledged, a.CreatedAtUtc))
            .ToListAsync(ct);
    }

    [HttpPost("alerts/{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var alert = await db.AiAlerts.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (alert is null || (!currentUser.IsSystemAdministrator && alert.DistrictId != currentUser.DistrictId))
            return NotFound();

        alert.IsAcknowledged = true;
        alert.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Run the prediction pipeline now (demo / admin action).</summary>
    [HttpPost("run")]
    [Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
    public async Task<ActionResult<AiRunResponse>> Run(CancellationToken ct)
    {
        var startedAt = await predictions.RunAsync(ct);
        await audit.WriteAsync("AiPipelineRun", nameof(PredictionSnapshot), null, ct: ct);
        return new AiRunResponse(startedAt);
    }
}
