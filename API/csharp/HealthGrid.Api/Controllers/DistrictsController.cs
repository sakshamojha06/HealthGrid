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
[Route("api/districts")]
public sealed class DistrictsController(
    HealthGridDbContext db, ICurrentUser currentUser, AuditWriter audit) : ControllerBase
{
    /// <summary>Districts visible to the caller (all for a system administrator, own district otherwise).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<DistrictDto>> List(CancellationToken ct)
    {
        var query = db.Districts.AsNoTracking().AsQueryable();
        if (!currentUser.IsSystemAdministrator)
            query = query.Where(d => d.Id == currentUser.DistrictId);

        return await query.OrderBy(d => d.Name)
            .Select(d => new DistrictDto(d.Id, d.Name, d.Code, d.Phcs.Count))
            .ToListAsync(ct);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<DistrictDto>> Create(DistrictInput input, CancellationToken ct)
    {
        if (await db.Districts.AnyAsync(d => d.Code == input.Code, ct))
            return Conflict("A district with that code already exists.");

        var district = new District { Name = input.Name.Trim(), Code = input.Code.Trim().ToUpperInvariant() };
        db.Districts.Add(district);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DistrictCreated", nameof(District), district.Id, ct: ct);
        return CreatedAtAction(nameof(List), new DistrictDto(district.Id, district.Name, district.Code, 0));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<DistrictDto>> Update(Guid id, DistrictInput input, CancellationToken ct)
    {
        var district = await db.Districts.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (district is null)
            return NotFound();

        district.Name = input.Name.Trim();
        district.Code = input.Code.Trim().ToUpperInvariant();
        district.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DistrictUpdated", nameof(District), id, ct: ct);

        var phcCount = await db.Phcs.CountAsync(p => p.DistrictId == id, ct);
        return new DistrictDto(district.Id, district.Name, district.Code, phcCount);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var district = await db.Districts.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (district is null)
            return NotFound();
        if (await db.Phcs.AnyAsync(p => p.DistrictId == id, ct))
            return Conflict("Remove the district's PHCs first.");

        db.Districts.Remove(district);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DistrictDeleted", nameof(District), id, ct: ct);
        return NoContent();
    }
}
