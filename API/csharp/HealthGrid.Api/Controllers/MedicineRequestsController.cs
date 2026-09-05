using HealthGrid.Api.Contracts;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/medicine-requests")]
public sealed class MedicineRequestsController(MedicineRequestService requests) : ControllerBase
{
    /// <summary>Incoming (my PHC supplies) or outgoing (my PHC requested) medicine requests.</summary>
    [HttpGet]
    public Task<IReadOnlyList<MedicineRequestDto>> List(
        [FromQuery] string direction = "incoming",
        [FromQuery] string? status = null,
        CancellationToken ct = default)
        => requests.ListAsync(direction, status, ct);

    /// <summary>PHCs in the same district with surplus stock of a medicine.</summary>
    [HttpGet("recommended-sources")]
    public Task<IReadOnlyList<RecommendedSourceDto>> RecommendedSources(
        [FromQuery] Guid medicineId, [FromQuery] decimal qty, CancellationToken ct)
        => requests.RecommendedSourcesAsync(medicineId, qty, ct);

    [HttpGet("{id:guid}")]
    public Task<MedicineRequestDto> Get(Guid id, CancellationToken ct) => requests.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<MedicineRequestDto>> Create(CreateMedicineRequestInput input, CancellationToken ct)
    {
        var created = await requests.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPost("{id:guid}/accept")]
    public Task<MedicineRequestDto> Accept(Guid id, CancellationToken ct) => requests.AcceptAsync(id, ct);

    [HttpPost("{id:guid}/reject")]
    public Task<MedicineRequestDto> Reject(Guid id, RejectRequestInput input, CancellationToken ct)
        => requests.RejectAsync(id, input.Reason, ct);

    [HttpPost("{id:guid}/fulfill")]
    public Task<MedicineRequestDto> Fulfill(Guid id, FulfillMedicineRequestInput input, CancellationToken ct)
        => requests.FulfillAsync(id, input, ct);

    [HttpPost("{id:guid}/complete")]
    public Task<MedicineRequestDto> Complete(Guid id, CancellationToken ct) => requests.CompleteAsync(id, ct);
}
