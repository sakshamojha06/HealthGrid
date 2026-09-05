using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using HealthGrid.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace HealthGrid.Api.Services;

/// <summary>
/// Persists a notification / AI alert and pushes it to the right SignalR groups.
/// Patient-level data is never broadcast — only operational summaries.
/// </summary>
public sealed class NotificationDispatcher(
    HealthGridDbContext db,
    IHubContext<NotificationsHub> hub)
{
    public async Task NotifyPhcAsync(
        Guid districtId, Guid phcId, string title, string message,
        AlertSeverity severity, CancellationToken ct)
    {
        var entity = new Notification
        {
            DistrictId = districtId,
            PhcId = phcId,
            Title = title,
            Message = message,
            Severity = severity,
        };
        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct);

        await hub.Clients.Group(NotificationsHub.PhcGroup(phcId))
            .SendAsync("notification", ToDto(entity), ct);
    }

    public async Task PushMedicineRequestAsync(Guid destinationPhcId, MedicineRequestDto dto, CancellationToken ct)
        => await hub.Clients.Group(NotificationsHub.PhcGroup(destinationPhcId))
            .SendAsync("medicineRequest", dto, ct);

    public async Task RaiseAiAlertAsync(AiAlert alert, CancellationToken ct)
    {
        db.AiAlerts.Add(alert);
        await db.SaveChangesAsync(ct);

        var target = alert.PhcId is { } phc
            ? hub.Clients.Group(NotificationsHub.PhcGroup(phc))
            : hub.Clients.Group(NotificationsHub.DistrictGroup(alert.DistrictId));

        await target.SendAsync("aiAlert", new AiAlertDto(
            alert.Id, alert.AlertType, alert.Message, alert.Severity.ToString(),
            alert.IsAcknowledged, alert.CreatedAtUtc), ct);
    }

    public static NotificationDto ToDto(Notification n) => new(
        n.Id, n.Title, n.Message, n.Severity.ToString(), n.IsAcknowledged, n.CreatedAtUtc);
}
