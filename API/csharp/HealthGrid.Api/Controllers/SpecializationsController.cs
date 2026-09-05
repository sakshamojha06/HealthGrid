using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Domain;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/specializations")]
public sealed class SpecializationsController(HealthGridDbContext db, AuditWriter audit) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SpecializationDto>> List(CancellationToken ct) =>
        await db.Specializations.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new SpecializationDto(s.Id, s.Name)).ToListAsync(ct);

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
    public async Task<ActionResult<SpecializationDto>> Create(SpecializationInput input, CancellationToken ct)
    {
        if (await db.Specializations.AnyAsync(s => s.Name == input.Name, ct))
            return Conflict("That specialization already exists.");

        var specialization = new Specialization { Name = input.Name.Trim() };
        db.Specializations.Add(specialization);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("SpecializationCreated", nameof(Specialization), specialization.Id, ct: ct);
        return new SpecializationDto(specialization.Id, specialization.Name);
    }
}
