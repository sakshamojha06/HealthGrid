using HealthGrid.Api.Auth;
using HealthGrid.Api.Contracts;
using HealthGrid.Api.Data;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.SystemAdministrator},{Roles.DistrictAdministrator}")]
[Route("api/users")]
public sealed class UsersController(
    HealthGridDbContext db,
    UserManager<ApplicationUser> users,
    ICurrentUser currentUser,
    AuditWriter audit) : ControllerBase
{
    [HttpGet]
    public async Task<PagedResult<UserAccountDto>> List(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Users.AsNoTracking().AsQueryable();
        if (!currentUser.IsSystemAdministrator)
            query = query.Where(u => u.DistrictId == currentUser.DistrictId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.FullName.Contains(search) || u.Email!.Contains(search));

        var total = await query.CountAsync(ct);
        var page1 = await query.OrderBy(u => u.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var items = new List<UserAccountDto>(page1.Count);
        foreach (var u in page1)
            items.Add(await ToDto(u));

        return new PagedResult<UserAccountDto>(items, page, pageSize, total);
    }

    [HttpPost]
    public async Task<ActionResult<UserAccountDto>> Create(CreateUserInput input, CancellationToken ct)
    {
        if (!Roles.All.Contains(input.Role))
            return BadRequest("Unknown role.");
        GuardAssignment(input.Role, input.DistrictId);

        if (await users.FindByEmailAsync(input.Email) is not null)
            return Conflict("A user with that email already exists.");
        if (input.DistrictId is { } d && !await db.Districts.AnyAsync(x => x.Id == d, ct))
            return BadRequest("Unknown district.");
        if (input.PhcId is { } p && !await db.Phcs.AnyAsync(x => x.Id == p && x.DistrictId == input.DistrictId, ct))
            return BadRequest("PHC does not belong to the given district.");

        var user = new ApplicationUser
        {
            UserName = input.Email.Trim(),
            Email = input.Email.Trim(),
            EmailConfirmed = true,
            FullName = input.FullName.Trim(),
            DistrictId = input.DistrictId,
            PhcId = input.PhcId,
        };
        var created = await users.CreateAsync(user, input.Password);
        if (!created.Succeeded)
            return BadRequest(string.Join(" ", created.Errors.Select(e => e.Description)));

        await users.AddToRoleAsync(user, input.Role);
        await audit.WriteAsync("UserCreated", nameof(ApplicationUser), user.Id, ct: ct);
        return await ToDto(user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserAccountDto>> Update(Guid id, UpdateUserInput input, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();
        if (!currentUser.IsSystemAdministrator && user.DistrictId != currentUser.DistrictId)
            return NotFound();

        if (input.Role is { } role)
        {
            if (!Roles.All.Contains(role))
                return BadRequest("Unknown role.");
            GuardAssignment(role, input.DistrictId ?? user.DistrictId);
            var currentRoles = await users.GetRolesAsync(user);
            await users.RemoveFromRolesAsync(user, currentRoles);
            await users.AddToRoleAsync(user, role);
        }

        if (input.FullName is { } name)
            user.FullName = name.Trim();
        if (input.DistrictId.HasValue)
            user.DistrictId = input.DistrictId;
        if (input.PhcId.HasValue)
            user.PhcId = input.PhcId;
        if (input.IsActive is { } active)
            user.IsActive = active;

        // Any identity / scope change invalidates outstanding tokens.
        user.TokenVersion++;
        await users.UpdateAsync(user);
        await audit.WriteAsync("UserUpdated", nameof(ApplicationUser), user.Id, ct: ct);
        return await ToDto(user);
    }

    private void GuardAssignment(string role, Guid? districtId)
    {
        if (currentUser.IsSystemAdministrator)
            return;
        // A district administrator may not mint system administrators or reach outside their district.
        if (role == Roles.SystemAdministrator)
            throw new UnauthorizedAccessException("You cannot assign the system administrator role.");
        if (districtId != currentUser.DistrictId)
            throw new UnauthorizedAccessException("You can only manage users in your own district.");
    }

    private async Task<UserAccountDto> ToDto(ApplicationUser user)
    {
        var role = (await users.GetRolesAsync(user)).FirstOrDefault() ?? Roles.PhcStaff;
        return new UserAccountDto(
            user.Id, user.Email ?? "", user.FullName, role,
            user.DistrictId, user.PhcId, user.IsActive);
    }
}
