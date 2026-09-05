using HealthGrid.Application.Inventory;
using HealthGrid.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(IInventoryService inventoryService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<InventoryDto>> Get(CancellationToken cancellationToken, [FromQuery] Guid? medicineId = null) =>
        inventoryService.GetAsync(medicineId, cancellationToken);

    [HttpPost("receipts")]
    public Task<InventoryDto> Receive([FromBody] InventoryMutationRequest request, CancellationToken cancellationToken) =>
        Mutate(request with { Type = InventoryTransactionType.Receipt }, cancellationToken);

    [HttpPost("issues")]
    public Task<InventoryDto> Issue([FromBody] InventoryMutationRequest request, CancellationToken cancellationToken) =>
        Mutate(request with { Type = InventoryTransactionType.PatientIssue }, cancellationToken);

    [HttpPost("adjustments")]
    public Task<InventoryDto> Adjust([FromBody] InventoryMutationRequest request, CancellationToken cancellationToken) =>
        Mutate(request with { Type = InventoryTransactionType.Adjustment }, cancellationToken);

    private Task<InventoryDto> Mutate(InventoryMutationRequest request, CancellationToken cancellationToken) =>
        inventoryService.MutateAsync(new InventoryMutation(request.DistrictId, request.PhcId, request.MedicineId, request.Quantity, request.Type, request.IdempotencyKey, request.Reference), cancellationToken);
}

public sealed record InventoryMutationRequest(Guid DistrictId, Guid PhcId, Guid MedicineId, decimal Quantity, string? IdempotencyKey, string? Reference, InventoryTransactionType Type = InventoryTransactionType.Receipt);
