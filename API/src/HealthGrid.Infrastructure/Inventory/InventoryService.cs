using HealthGrid.Application.Abstractions;
using HealthGrid.Application.Inventory;
using HealthGrid.Domain.Entities;
using HealthGrid.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Infrastructure.Inventory;

public sealed class InventoryService(HealthGridDbContext db, IUserScope userScope) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryDto>> GetAsync(Guid? medicineId, CancellationToken cancellationToken)
    {
        var scope = userScope.Current;
        var query = db.MedicineInventories.AsNoTracking()
            .Where(x => scope.IsSystemAdministrator || x.DistrictId == scope.DistrictId && (scope.PhcId == null || x.PhcId == scope.PhcId));
        if (medicineId.HasValue) query = query.Where(x => x.MedicineId == medicineId.Value);
        return await query.OrderBy(x => x.MedicineId)
            .Select(x => new InventoryDto(x.Id, x.DistrictId, x.PhcId, x.MedicineId, x.QuantityOnHand, x.SafetyStock, x.ExpiryDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<InventoryDto> MutateAsync(InventoryMutation mutation, CancellationToken cancellationToken)
    {
        var scope = userScope.Current;
        if (!scope.CanAccess(mutation.DistrictId, mutation.PhcId)) throw new UnauthorizedAccessException("The requested PHC is outside the current scope.");
        if (mutation.Quantity <= 0) throw new ArgumentOutOfRangeException(nameof(mutation.Quantity), "Quantity must be positive.");
        if (mutation.Type == InventoryTransactionType.Adjustment && mutation.Quantity == 0) throw new ArgumentOutOfRangeException(nameof(mutation.Quantity));

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(mutation.IdempotencyKey))
        {
            var existing = await db.InventoryTransactions.AsNoTracking().FirstOrDefaultAsync(x =>
                x.PhcId == mutation.PhcId && x.IdempotencyKey == mutation.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                var current = await db.MedicineInventories.AsNoTracking().SingleAsync(x => x.Id == existing.ReferenceId(), cancellationToken);
                return ToDto(current);
            }
        }

        var inventory = await db.MedicineInventories.SingleOrDefaultAsync(x =>
            x.DistrictId == mutation.DistrictId && x.PhcId == mutation.PhcId && x.MedicineId == mutation.MedicineId, cancellationToken);
        if (inventory is null)
        {
            inventory = new MedicineInventory { DistrictId = mutation.DistrictId, PhcId = mutation.PhcId, MedicineId = mutation.MedicineId };
            db.MedicineInventories.Add(inventory);
        }

        var increases = mutation.Type is InventoryTransactionType.Receipt or InventoryTransactionType.TransferIn;
        var decreases = mutation.Type is InventoryTransactionType.PatientIssue or InventoryTransactionType.TransferOut;
        var balance = increases ? inventory.QuantityOnHand + mutation.Quantity : decreases ? inventory.QuantityOnHand - mutation.Quantity : inventory.QuantityOnHand + mutation.Quantity;
        if (balance < 0) throw new InvalidOperationException("Inventory cannot become negative.");
        inventory.QuantityOnHand = balance;
        inventory.UpdatedAtUtc = DateTime.UtcNow;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            DistrictId = mutation.DistrictId, PhcId = mutation.PhcId, MedicineId = mutation.MedicineId,
            Type = mutation.Type, Quantity = mutation.Quantity, BalanceAfter = balance,
            IdempotencyKey = mutation.IdempotencyKey, Reference = inventory.Id.ToString()
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(inventory);
    }

    private static InventoryDto ToDto(MedicineInventory x) => new(x.Id, x.DistrictId, x.PhcId, x.MedicineId, x.QuantityOnHand, x.SafetyStock, x.ExpiryDate);
}

file static class InventoryTransactionExtensions
{
    public static Guid ReferenceId(this InventoryTransaction transaction) => Guid.Parse(transaction.Reference!);
}
