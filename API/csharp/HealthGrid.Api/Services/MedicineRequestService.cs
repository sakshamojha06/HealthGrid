using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

public sealed class MedicineRequestError(string message) : Exception(message);

/// <summary>
/// PHC-to-PHC medicine requests. Rules enforced here:
/// same-district only, no self-transfer, stock safeguards on fulfilment,
/// idempotent completion, an audit row and notification on every state change.
/// </summary>
public sealed class MedicineRequestService(
    HealthGridDbContext db,
    ICurrentUser currentUser,
    InventoryService inventory,
    NotificationDispatcher notifications,
    AuditWriter audit)
{
    public async Task<MedicineRequestDto> CreateAsync(CreateMedicineRequestInput input, CancellationToken ct)
    {
        if (currentUser.PhcId is not { } sourcePhcId)
            throw new MedicineRequestError("Only a PHC-assigned user can raise a medicine request.");
        if (input.Items is not { Count: > 0 })
            throw new MedicineRequestError("At least one medicine line is required.");
        if (input.DestinationPhcId == sourcePhcId)
            throw new MedicineRequestError("A PHC cannot request stock from itself.");

        var source = await db.Phcs.SingleOrDefaultAsync(p => p.Id == sourcePhcId, ct)
                     ?? throw new MedicineRequestError("Unknown source PHC.");
        var destination = await db.Phcs.SingleOrDefaultAsync(p => p.Id == input.DestinationPhcId, ct)
                          ?? throw new MedicineRequestError("Unknown destination PHC.");
        if (source.DistrictId != destination.DistrictId)
            throw new MedicineRequestError("Cross-district medicine exchange is not permitted.");

        currentUser.EnsureAccess(source.DistrictId, sourcePhcId);

        var fullName = await db.Users.Where(u => u.Id == currentUser.UserId)
            .Select(u => u.FullName).SingleOrDefaultAsync(ct) ?? currentUser.Email ?? "Unknown";

        var request = new MedicineRequest
        {
            DistrictId = source.DistrictId,
            SourcePhcId = sourcePhcId,
            DestinationPhcId = destination.Id,
            Status = MedicineRequestStatus.Pending,
            Notes = input.Notes,
            NeededByDate = input.NeededByDate,
            RequestedByUserId = currentUser.UserId,
            RequestedByName = fullName,
            Items = input.Items.Select(i => new MedicineRequestItem
            {
                MedicineId = i.MedicineId,
                QuantityRequested = i.QuantityRequested,
            }).ToList(),
        };
        db.MedicineRequests.Add(request);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineRequestCreated", nameof(MedicineRequest), request.Id, ct: ct);

        var dto = await ProjectAsync(request.Id, ct);
        await notifications.PushMedicineRequestAsync(destination.Id, dto, ct);
        await notifications.NotifyPhcAsync(destination.DistrictId, destination.Id,
            "New medicine request",
            $"{source.Name} has requested stock from your PHC.",
            AlertSeverity.Info, ct);
        return dto;
    }

    public async Task<IReadOnlyList<MedicineRequestDto>> ListAsync(
        string direction, string? status, CancellationToken ct)
    {
        var myPhc = currentUser.PhcId;
        var query = ScopedRequests();

        query = direction.Equals("incoming", StringComparison.OrdinalIgnoreCase)
            ? query.Where(r => r.DestinationPhcId == myPhc)
            : query.Where(r => r.SourcePhcId == myPhc);

        if (Enum.TryParse<MedicineRequestStatus>(status, out var parsed))
            query = query.Where(r => r.Status == parsed);

        var ids = await query.OrderByDescending(r => r.CreatedAtUtc).Select(r => r.Id).ToListAsync(ct);
        var list = new List<MedicineRequestDto>(ids.Count);
        foreach (var id in ids)
            list.Add(await ProjectAsync(id, ct));
        return list;
    }

    public async Task<MedicineRequestDto> GetAsync(Guid id, CancellationToken ct)
    {
        var request = await LoadAsync(id, ct);
        return await ProjectAsync(request.Id, ct);
    }

    public async Task<MedicineRequestDto> AcceptAsync(Guid id, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);
        if (request.Status != MedicineRequestStatus.Pending)
            throw new MedicineRequestError("Only a pending request can be accepted.");
        request.Status = MedicineRequestStatus.Accepted;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineRequestAccepted", nameof(MedicineRequest), id, ct: ct);
        await NotifySourceAsync(request, "Medicine request accepted",
            "The supplying PHC has accepted your request.", ct);
        return await ProjectAsync(id, ct);
    }

    public async Task<MedicineRequestDto> RejectAsync(Guid id, string? reason, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);
        if (request.Status is MedicineRequestStatus.Completed or MedicineRequestStatus.Rejected)
            throw new MedicineRequestError("This request can no longer be rejected.");
        request.Status = MedicineRequestStatus.Rejected;
        request.DecisionReason = reason;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineRequestRejected", nameof(MedicineRequest), id,
            reason is null ? null : $"{{\"reason\":\"{reason}\"}}", ct);
        await NotifySourceAsync(request, "Medicine request rejected",
            reason is { Length: > 0 } ? $"Rejected: {reason}" : "The supplying PHC rejected your request.", ct);
        return await ProjectAsync(id, ct);
    }

    public async Task<MedicineRequestDto> FulfillAsync(
        Guid id, FulfillMedicineRequestInput input, CancellationToken ct)
    {
        var request = await LoadForDecisionAsync(id, ct);
        if (request.Status is not (MedicineRequestStatus.Accepted or MedicineRequestStatus.PartiallyFulfilled))
            throw new MedicineRequestError("Accept the request before fulfilling it.");

        var lines = input.Items?.Where(i => i.QuantityFulfilled > 0).ToList() ?? [];
        if (lines.Count == 0)
            throw new MedicineRequestError("Nothing to fulfil.");

        foreach (var line in lines)
        {
            var item = request.Items.SingleOrDefault(x => x.MedicineId == line.MedicineId)
                       ?? throw new MedicineRequestError("Medicine is not part of this request.");

            var outstanding = item.QuantityRequested - item.QuantityFulfilled;
            var move = Math.Min(line.QuantityFulfilled, outstanding);
            if (move <= 0)
                continue;

            var key = $"transfer-{request.Id}-{item.MedicineId}-{item.QuantityFulfilled}";

            // Supplying PHC releases stock; requesting PHC receives it.
            await inventory.ApplyAsync(request.DestinationPhcId, item.MedicineId, -move,
                InventoryTransactionType.TransferOut, $"Request {request.Id}", key + "-out", ct, enforceScope: false);
            await inventory.ApplyAsync(request.SourcePhcId, item.MedicineId, move,
                InventoryTransactionType.TransferIn, $"Request {request.Id}", key + "-in", ct, enforceScope: false);

            item.QuantityFulfilled += move;
        }

        var allMet = request.Items.All(x => x.QuantityFulfilled >= x.QuantityRequested);
        request.Status = allMet ? MedicineRequestStatus.Fulfilled : MedicineRequestStatus.PartiallyFulfilled;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineRequestFulfilled", nameof(MedicineRequest), id, ct: ct);
        await NotifySourceAsync(request, "Stock transferred",
            "The supplying PHC has transferred stock for your request.", ct);
        return await ProjectAsync(id, ct);
    }

    public async Task<MedicineRequestDto> CompleteAsync(Guid id, CancellationToken ct)
    {
        var request = await LoadAsync(id, ct);
        EnsureParticipant(request);

        if (request.Status == MedicineRequestStatus.Completed)
            return await ProjectAsync(id, ct); // idempotent

        if (request.Status is not (MedicineRequestStatus.Fulfilled or MedicineRequestStatus.PartiallyFulfilled))
            throw new MedicineRequestError("Only a fulfilled request can be completed.");

        request.Status = MedicineRequestStatus.Completed;
        request.CompletedAtUtc = DateTime.UtcNow;
        request.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineRequestCompleted", nameof(MedicineRequest), id, ct: ct);
        return await ProjectAsync(id, ct);
    }

    public async Task<IReadOnlyList<RecommendedSourceDto>> RecommendedSourcesAsync(
        Guid medicineId, decimal qty, CancellationToken ct)
    {
        if (currentUser.DistrictId is not { } districtId)
            return [];
        var myPhc = currentUser.PhcId;

        var candidates = await db.MedicineInventories.AsNoTracking()
            .Where(x => x.DistrictId == districtId && x.MedicineId == medicineId && x.PhcId != myPhc)
            .Where(x => x.QuantityOnHand - x.SafetyStock > 0)
            .Join(db.Phcs.AsNoTracking(), inv => inv.PhcId, p => p.Id,
                (inv, p) => new { p.Id, p.Name, Surplus = inv.QuantityOnHand - inv.SafetyStock })
            .ToListAsync(ct);

        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14));
        var results = new List<RecommendedSourceDto>();
        foreach (var c in candidates)
        {
            var issued = await db.InventoryTransactions.AsNoTracking()
                .Where(t => t.PhcId == c.Id && t.MedicineId == medicineId
                            && t.Type == InventoryTransactionType.PatientIssue
                            && t.CreatedAtUtc >= since.ToDateTime(TimeOnly.MinValue))
                .SumAsync(t => (decimal?)-t.Quantity, ct) ?? 0m;
            var predicted7d = Math.Round(issued / 14m * 7m, 2);
            results.Add(new RecommendedSourceDto(c.Id, c.Name, Math.Round(c.Surplus, 2), predicted7d));
        }

        return results
            .OrderByDescending(r => r.AvailableSurplus - r.PredictedDemand7d)
            .Take(5).ToList();
    }

    // -- helpers -----------------------------------------------------------

    private IQueryable<MedicineRequest> ScopedRequests()
    {
        var q = db.MedicineRequests.AsNoTracking();
        if (currentUser.IsSystemAdministrator)
            return q;
        var district = currentUser.DistrictId;
        return q.Where(r => r.DistrictId == district);
    }

    private async Task<MedicineRequest> LoadAsync(Guid id, CancellationToken ct)
    {
        var request = await db.MedicineRequests.Include(r => r.Items)
            .SingleOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundError("Request not found.");

        // 404-style: hide existence of out-of-scope requests.
        if (!currentUser.IsSystemAdministrator && request.DistrictId != currentUser.DistrictId)
            throw new NotFoundError("Request not found.");
        return request;
    }

    private async Task<MedicineRequest> LoadForDecisionAsync(Guid id, CancellationToken ct)
    {
        var request = await LoadAsync(id, ct);
        if (!currentUser.IsSystemAdministrator
            && !currentUser.IsInRole(Roles.DistrictAdministrator)
            && currentUser.PhcId != request.DestinationPhcId)
        {
            throw new MedicineRequestError("Only the supplying PHC can act on this request.");
        }
        return request;
    }

    private void EnsureParticipant(MedicineRequest request)
    {
        if (currentUser.IsSystemAdministrator || currentUser.IsInRole(Roles.DistrictAdministrator))
            return;
        if (currentUser.PhcId != request.SourcePhcId && currentUser.PhcId != request.DestinationPhcId)
            throw new MedicineRequestError("You are not a participant in this request.");
    }

    private async Task NotifySourceAsync(MedicineRequest request, string title, string message, CancellationToken ct)
        => await notifications.NotifyPhcAsync(request.DistrictId, request.SourcePhcId, title, message, AlertSeverity.Info, ct);

    private async Task<MedicineRequestDto> ProjectAsync(Guid id, CancellationToken ct)
    {
        var r = await db.MedicineRequests.AsNoTracking()
            .Include(x => x.Items).ThenInclude(i => i.Medicine)
            .Include(x => x.SourcePhc)
            .Include(x => x.DestinationPhc)
            .SingleAsync(x => x.Id == id, ct);

        return new MedicineRequestDto(
            r.Id, r.SourcePhcId, r.SourcePhc!.Name, r.DestinationPhcId, r.DestinationPhc!.Name,
            r.Status.ToString(), r.Notes, r.NeededByDate, r.RequestedByName, r.CreatedAtUtc,
            r.Items.Select(i => new MedicineRequestItemDto(
                i.MedicineId, i.Medicine!.Name, i.QuantityRequested, i.QuantityFulfilled)).ToList());
    }
}
