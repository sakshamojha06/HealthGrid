using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Domain;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/inventory")]
public sealed class InventoryController(InventoryService inventory, AuditWriter audit) : ControllerBase
{
    /// <summary>District-wide stock levels so a PHC can see what its neighbours hold.</summary>
    [HttpGet]
    public Task<IReadOnlyList<InventoryRowDto>> Stock(
        [FromQuery] Guid? phcId = null,
        [FromQuery] Guid? medicineId = null,
        [FromQuery] bool lowOnly = false,
        CancellationToken ct = default)
        => inventory.GetStockAsync(phcId, medicineId, lowOnly, ct);

    /// <summary>The immutable transaction ledger, newest first.</summary>
    [HttpGet("transactions")]
    public Task<PagedResult<InventoryTransactionDto>> Transactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? phcId = null,
        [FromQuery] Guid? medicineId = null,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
        => inventory.GetTransactionsAsync(page, pageSize, phcId, medicineId, type, ct);

    /// <summary>Receive stock into a PHC (positive quantity).</summary>
    [HttpPost("receipts")]
    [Authorize(Roles = $"{Roles.PhcStaff},{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public Task<ActionResult<InventoryRowDto>> Receive(StockMutationRequest request, CancellationToken ct)
        => Apply(request, Math.Abs(request.Quantity), InventoryTransactionType.Receipt, ct);

    /// <summary>Apply a signed correction to a PHC's stock (positive or negative).</summary>
    [HttpPost("adjustments")]
    [Authorize(Roles = $"{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public Task<ActionResult<InventoryRowDto>> Adjust(StockMutationRequest request, CancellationToken ct)
        => Apply(request, request.Quantity, InventoryTransactionType.Adjustment, ct);

    /// <summary>Issue stock out of a PHC directly (positive quantity, recorded as a negative delta).</summary>
    [HttpPost("issues")]
    [Authorize(Roles = $"{Roles.Doctor},{Roles.PhcStaff},{Roles.PhcAdministrator}")]
    public Task<ActionResult<InventoryRowDto>> Issue(StockMutationRequest request, CancellationToken ct)
        => Apply(request, -Math.Abs(request.Quantity), InventoryTransactionType.PatientIssue, ct);

    private async Task<ActionResult<InventoryRowDto>> Apply(
        StockMutationRequest request, decimal signedQuantity, InventoryTransactionType type, CancellationToken ct)
    {
        var inv = await inventory.ApplyAsync(
            request.PhcId, request.MedicineId, signedQuantity, type, request.Reference, request.IdempotencyKey, ct);
        await audit.WriteAsync($"Inventory{type}", nameof(MedicineInventory), inv.Id, ct: ct);
        return await inventory.ToRowAsync(inv, ct);
    }
}
