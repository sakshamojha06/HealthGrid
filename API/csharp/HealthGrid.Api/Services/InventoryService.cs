using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Services;

public sealed class InventoryError(string message) : Exception(message);

/// <summary>
/// The single choke-point for every stock change. All mutations run inside a
/// database transaction, honour an optional idempotency key, append an immutable
/// ledger row, and refuse to let a balance go negative.
/// </summary>
public sealed class InventoryService(HealthGridDbContext db, ICurrentUser currentUser)
{
    public async Task<IReadOnlyList<InventoryRowDto>> GetStockAsync(
        Guid? phcId, Guid? medicineId, bool lowOnly, CancellationToken ct)
    {
        var query = ScopedInventory();
        if (phcId is { } p)
        {
            currentUser.EnsureAccess(await DistrictOfPhc(p, ct), p);
            query = query.Where(x => x.PhcId == p);
        }
        if (medicineId is { } m)
            query = query.Where(x => x.MedicineId == m);

        var rows = await (
            from x in query
            join ph in db.Phcs.AsNoTracking() on x.PhcId equals ph.Id
            orderby x.Medicine!.Name, ph.Name
            select new InventoryRowDto(
                x.Id, x.DistrictId, x.PhcId, ph.Name,
                x.MedicineId, x.Medicine!.Name, x.Medicine.Unit,
                x.QuantityOnHand, x.SafetyStock, x.ExpiryDate,
                x.QuantityOnHand <= x.SafetyStock))
            .ToListAsync(ct);

        return lowOnly ? rows.Where(r => r.IsLow).ToList() : rows;
    }

    public async Task<PagedResult<InventoryTransactionDto>> GetTransactionsAsync(
        int page, int pageSize, Guid? phcId, Guid? medicineId, string? type, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.InventoryTransactions.AsNoTracking()
            .Where(InScope());
        if (phcId is { } p)
            query = query.Where(x => x.PhcId == p);
        if (medicineId is { } m)
            query = query.Where(x => x.MedicineId == m);
        if (Enum.TryParse<InventoryTransactionType>(type, out var t))
            query = query.Where(x => x.Type == t);

        var total = await query.CountAsync(ct);
        var items = await (
            from x in query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            join ph in db.Phcs.AsNoTracking() on x.PhcId equals ph.Id
            join med in db.Medicines.AsNoTracking() on x.MedicineId equals med.Id
            select new InventoryTransactionDto(
                x.Id, x.PhcId, ph.Name, med.Name, x.Type.ToString(), x.Quantity, x.BalanceAfter,
                x.Reference, x.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<InventoryTransactionDto>(items, page, pageSize, total);
    }

    /// <summary>
    /// Applies a signed delta to one PHC's stock and records it in the ledger.
    /// <paramref name="signedQuantity"/> is negative for issues / reducing adjustments.
    /// </summary>
    public async Task<MedicineInventory> ApplyAsync(
        Guid phcId, Guid medicineId, decimal signedQuantity, InventoryTransactionType type,
        string? reference, string? idempotencyKey, CancellationToken ct, bool enforceScope = true)
    {
        if (signedQuantity == 0)
            throw new InventoryError("Quantity must not be zero.");
        if (type is InventoryTransactionType.Receipt or InventoryTransactionType.TransferIn && signedQuantity < 0)
            throw new InventoryError("Receipts must be positive.");
        if (type is InventoryTransactionType.PatientIssue or InventoryTransactionType.TransferOut && signedQuantity > 0)
            throw new InventoryError("Issues must be negative.");

        var districtId = await DistrictOfPhc(phcId, ct);
        if (enforceScope)
            currentUser.EnsureAccess(districtId, phcId);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var seen = await db.InventoryTransactions.AsNoTracking()
                    .AnyAsync(x => x.PhcId == phcId && x.IdempotencyKey == idempotencyKey, ct);
                if (seen)
                {
                    await tx.RollbackAsync(ct);
                    return await db.MedicineInventories.AsNoTracking()
                        .SingleAsync(x => x.PhcId == phcId && x.MedicineId == medicineId, ct);
                }
            }

            var inventory = await db.MedicineInventories
                .SingleOrDefaultAsync(x => x.PhcId == phcId && x.MedicineId == medicineId, ct);
            if (inventory is null)
            {
                inventory = new MedicineInventory
                {
                    DistrictId = districtId,
                    PhcId = phcId,
                    MedicineId = medicineId,
                };
                db.MedicineInventories.Add(inventory);
            }

            var balance = inventory.QuantityOnHand + signedQuantity;
            if (balance < 0)
                throw new InventoryError("This change would make stock negative.");

            inventory.QuantityOnHand = balance;
            inventory.UpdatedAtUtc = DateTime.UtcNow;

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                DistrictId = districtId,
                PhcId = phcId,
                MedicineId = medicineId,
                Type = type,
                Quantity = signedQuantity,
                BalanceAfter = balance,
                Reference = reference,
                IdempotencyKey = idempotencyKey,
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return inventory;
        });
    }

    public async Task<InventoryRowDto> ToRowAsync(MedicineInventory inv, CancellationToken ct)
    {
        var medicine = await db.Medicines.AsNoTracking().SingleAsync(m => m.Id == inv.MedicineId, ct);
        var phcName = await db.Phcs.AsNoTracking().Where(p => p.Id == inv.PhcId).Select(p => p.Name).SingleAsync(ct);
        return new InventoryRowDto(
            inv.Id, inv.DistrictId, inv.PhcId, phcName, inv.MedicineId, medicine.Name, medicine.Unit,
            inv.QuantityOnHand, inv.SafetyStock, inv.ExpiryDate, inv.QuantityOnHand <= inv.SafetyStock);
    }

    private IQueryable<MedicineInventory> ScopedInventory()
    {
        var q = db.MedicineInventories.AsNoTracking().Include(x => x.Medicine).AsQueryable();
        if (currentUser.IsSystemAdministrator)
            return q;
        // PHC-level users may view the whole district so they know what neighbours hold.
        return q.Where(x => x.DistrictId == currentUser.DistrictId);
    }

    private System.Linq.Expressions.Expression<Func<InventoryTransaction, bool>> InScope()
    {
        if (currentUser.IsSystemAdministrator)
            return _ => true;
        var district = currentUser.DistrictId;
        return x => x.DistrictId == district;
    }

    private async Task<Guid> DistrictOfPhc(Guid phcId, CancellationToken ct)
    {
        var district = await db.Phcs.Where(p => p.Id == phcId).Select(p => p.DistrictId).SingleOrDefaultAsync(ct);
        return district != Guid.Empty ? district : throw new InventoryError("Unknown PHC.");
    }
}
