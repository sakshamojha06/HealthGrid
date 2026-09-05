using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/patient-visits")]
public sealed class PatientVisitsController(VisitService visits) : ControllerBase
{
    /// <summary>Scoped list of recorded visits with filters and pagination.</summary>
    [HttpGet]
    public Task<PagedResult<PatientVisitDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] Guid? diseaseId = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => visits.ListAsync(page, pageSize, from, to, diseaseId, search, ct);

    [HttpGet("{id:guid}")]
    public Task<PatientVisitDto> Get(Guid id, CancellationToken ct) => visits.GetAsync(id, ct);

    /// <summary>Record a visit for the caller's PHC and issue the prescribed stock.</summary>
    [HttpPost]
    [Authorize(Roles = $"{Roles.Doctor},{Roles.PhcStaff},{Roles.PhcAdministrator}")]
    public async Task<ActionResult<PatientVisitDto>> Record(RecordVisitRequest request, CancellationToken ct)
    {
        var visit = await visits.RecordAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = visit.Id }, visit);
    }
}
