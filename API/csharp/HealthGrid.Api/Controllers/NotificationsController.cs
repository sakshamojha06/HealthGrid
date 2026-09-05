using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController(HealthGridDbContext db, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>The 100 most recent notifications addressed to the caller or their PHC.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<NotificationDto>> List(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        var phcId = currentUser.PhcId;
        var districtId = currentUser.DistrictId;

        var query = db.Notifications.AsNoTracking().Where(n =>
            n.UserId == userId
            || (phcId != null && n.PhcId == phcId)
            || (phcId == null && districtId != null && n.DistrictId == districtId && n.PhcId == null));

        return await query.OrderByDescending(n => n.CreatedAtUtc).Take(100)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Message, n.Severity.ToString(), n.IsAcknowledged, n.CreatedAtUtc))
            .ToListAsync(ct);
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id, ct);
        if (notification is null)
            return NotFound();

        var canSee = notification.UserId == currentUser.UserId
                     || (currentUser.PhcId is { } p && notification.PhcId == p)
                     || (currentUser.DistrictId is { } d && notification.DistrictId == d);
        if (!canSee && !currentUser.IsSystemAdministrator)
            return NotFound();

        notification.IsAcknowledged = true;
        notification.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
