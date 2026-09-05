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
[Route("api/phcs")]
public sealed class PhcsController(
    HealthGridDbContext db, ICurrentUser currentUser, AuditWriter audit) : ControllerBase
{
    /// <summary>PHCs in scope. Non-system users only ever see their own district's PHCs.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<PhcDto>> List([FromQuery] Guid? districtId, CancellationToken ct)
    {
        var query = db.Phcs.AsNoTracking().Include(p => p.District).AsQueryable();

        if (!currentUser.IsSystemAdministrator)
            query = query.Where(p => p.DistrictId == currentUser.DistrictId);
        if (districtId is { } d)
            query = query.Where(p => p.DistrictId == d);

        return await query.OrderBy(p => p.Name)
            .Select(p => new PhcDto(p.Id, p.DistrictId, p.District!.Name, p.Name, p.Code))
            .ToListAsync(ct);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
    public async Task<ActionResult<PhcDto>> Create(PhcInput input, CancellationToken ct)
    {
        currentUser.EnsureAccess(input.DistrictId);
        if (!await db.Districts.AnyAsync(d => d.Id == input.DistrictId, ct))
            return NotFound("Unknown district.");
        if (await db.Phcs.AnyAsync(p => p.DistrictId == input.DistrictId && p.Code == input.Code, ct))
            return Conflict("A PHC with that code already exists in this district.");

        var phc = new Phc
        {
            DistrictId = input.DistrictId,
            Name = input.Name.Trim(),
            Code = input.Code.Trim().ToUpperInvariant(),
        };
        db.Phcs.Add(phc);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PhcCreated", nameof(Phc), phc.Id, ct: ct);

        var districtName = await db.Districts.Where(d => d.Id == phc.DistrictId).Select(d => d.Name).SingleAsync(ct);
        return CreatedAtAction(nameof(List), new PhcDto(phc.Id, phc.DistrictId, districtName, phc.Name, phc.Code));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
    public async Task<ActionResult<PhcDto>> Update(Guid id, PhcInput input, CancellationToken ct)
    {
        var phc = await db.Phcs.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (phc is null)
            return NotFound();
        currentUser.EnsureAccess(phc.DistrictId);
        currentUser.EnsureAccess(input.DistrictId);

        phc.DistrictId = input.DistrictId;
        phc.Name = input.Name.Trim();
        phc.Code = input.Code.Trim().ToUpperInvariant();
        phc.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PhcUpdated", nameof(Phc), id, ct: ct);

        var districtName = await db.Districts.Where(d => d.Id == phc.DistrictId).Select(d => d.Name).SingleAsync(ct);
        return new PhcDto(phc.Id, phc.DistrictId, districtName, phc.Name, phc.Code);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var phc = await db.Phcs.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (phc is null)
            return NotFound();
        currentUser.EnsureAccess(phc.DistrictId);

        if (await db.PatientVisits.AnyAsync(v => v.PhcId == id, ct)
            || await db.MedicineInventories.AnyAsync(m => m.PhcId == id, ct))
        {
            return Conflict("This PHC has operational records and cannot be deleted.");
        }

        db.Phcs.Remove(phc);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PhcDeleted", nameof(Phc), id, ct: ct);
        return NoContent();
    }
}
