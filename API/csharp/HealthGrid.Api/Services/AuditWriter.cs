using HealthGrid.Api.Auth;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;

namespace HealthGrid.Api.Services;

/// <summary>Appends an audit row for a state-changing operation.</summary>
public sealed class AuditWriter(HealthGridDbContext db, ICurrentUser currentUser)
{
    public async Task WriteAsync(
        string action, string entityName, Guid? entityId = null,
        string? detailsJson = null, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            DistrictId = currentUser.DistrictId,
            PhcId = currentUser.PhcId,
            ActorUserId = currentUser.UserId == Guid.Empty ? null : currentUser.UserId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            DetailsJson = detailsJson,
        });
        await db.SaveChangesAsync(ct);
    }
}
