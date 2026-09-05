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
[Route("api/medicines")]
public sealed class MedicinesController(HealthGridDbContext db, AuditWriter audit) : ControllerBase
{
    /// <summary>Paged medicine catalogue. Shared reference data — visible to every authenticated user.</summary>
    [HttpGet]
    public async Task<PagedResult<MedicineDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Medicines.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.Name.Contains(search) || m.GenericName.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new MedicineDto(m.Id, m.Name, m.GenericName, m.Unit, m.IsActive))
            .ToListAsync(ct);

        return new PagedResult<MedicineDto>(items, page, pageSize, total);
    }

    [HttpPost]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<MedicineDto>> Create(MedicineInput input, CancellationToken ct)
    {
        var medicine = new Medicine
        {
            Name = input.Name.Trim(),
            GenericName = input.GenericName.Trim(),
            Unit = input.Unit.Trim(),
            IsActive = input.IsActive,
        };
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineCreated", nameof(Medicine), medicine.Id, ct: ct);
        return new MedicineDto(medicine.Id, medicine.Name, medicine.GenericName, medicine.Unit, medicine.IsActive);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.SystemAdministrator)]
    public async Task<ActionResult<MedicineDto>> Update(Guid id, MedicineInput input, CancellationToken ct)
    {
        var medicine = await db.Medicines.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (medicine is null)
            return NotFound();

        medicine.Name = input.Name.Trim();
        medicine.GenericName = input.GenericName.Trim();
        medicine.Unit = input.Unit.Trim();
        medicine.IsActive = input.IsActive;
        medicine.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MedicineUpdated", nameof(Medicine), id, ct: ct);
        return new MedicineDto(medicine.Id, medicine.Name, medicine.GenericName, medicine.Unit, medicine.IsActive);
    }
}
