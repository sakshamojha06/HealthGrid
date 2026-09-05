using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/doctors")]
public sealed class DoctorsController(DoctorService doctors) : ControllerBase
{
    /// <summary>Search the district's doctors by specialization, PHC or available day.</summary>
    [HttpGet("search")]
    public Task<IReadOnlyList<DoctorDto>> Search(
        [FromQuery] Guid? specializationId = null,
        [FromQuery] Guid? phcId = null,
        [FromQuery] int? day = null,
        CancellationToken ct = default)
        => doctors.SearchAsync(specializationId, phcId, day, ct);

    [HttpGet("{id:guid}")]
    public Task<DoctorDto> Get(Guid id, CancellationToken ct) => doctors.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = $"{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public async Task<ActionResult<DoctorDto>> Create(DoctorInput input, CancellationToken ct)
    {
        var doctor = await doctors.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = doctor.Id }, doctor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public Task<DoctorDto> Update(Guid id, DoctorInput input, CancellationToken ct)
        => doctors.UpdateAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await doctors.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/availability")]
    [Authorize(Roles = $"{Roles.PhcAdministrator},{Roles.DistrictAdministrator},{Roles.SystemAdministrator}")]
    public Task<DoctorDto> SetAvailability(Guid id, SetAvailabilityInput input, CancellationToken ct)
        => doctors.SetAvailabilityAsync(id, input, ct);
}
