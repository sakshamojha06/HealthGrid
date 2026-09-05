using HealthGrid.Domain.Entities;

namespace HealthGrid.Application.Inventory;

public sealed record InventoryDto(Guid Id, Guid DistrictId, Guid PhcId, Guid MedicineId, decimal QuantityOnHand, decimal SafetyStock, DateOnly? ExpiryDate);
public sealed record InventoryMutation(Guid DistrictId, Guid PhcId, Guid MedicineId, decimal Quantity, InventoryTransactionType Type, string? IdempotencyKey, string? Reference);
public interface IInventoryService
{
    Task<IReadOnlyList<InventoryDto>> GetAsync(Guid? medicineId, CancellationToken cancellationToken);
    Task<InventoryDto> MutateAsync(InventoryMutation mutation, CancellationToken cancellationToken);
}
