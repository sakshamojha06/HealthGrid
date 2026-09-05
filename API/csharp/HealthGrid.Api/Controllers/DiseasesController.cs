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
[Route("api/diseases")]
public sealed class DiseasesController(HealthGridDbContext db, AuditWriter audit) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<DiseaseDto>> List(CancellationToken ct) =>
        await db.Diseases.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new DiseaseDto(d.Id, d.Name, d.Code)).ToListAsync(ct);

    [HttpPost]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<DiseaseDto>> Create(DiseaseInput input, CancellationToken ct)
    {
        if (await db.Diseases.AnyAsync(d => d.Code == input.Code, ct))
            return Conflict("A disease with that code already exists.");

        var disease = new Disease { Name = input.Name.Trim(), Code = input.Code.Trim().ToUpperInvariant() };
        db.Diseases.Add(disease);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DiseaseCreated", nameof(Disease), disease.Id, ct: ct);
        return new DiseaseDto(disease.Id, disease.Name, disease.Code);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<DiseaseDto>> Update(Guid id, DiseaseInput input, CancellationToken ct)
    {
        var disease = await db.Diseases.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (disease is null)
            return NotFound();

        disease.Name = input.Name.Trim();
        disease.Code = input.Code.Trim().ToUpperInvariant();
        disease.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DiseaseUpdated", nameof(Disease), id, ct: ct);
        return new DiseaseDto(disease.Id, disease.Name, disease.Code);
    }
}
