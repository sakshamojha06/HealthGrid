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
[Route("api/auth")]
public sealed class AuthController(
    HealthGridDbContext db,
    UserManager<ApplicationUser> users,
    TokenService tokens,
    ICurrentUser currentUser,
    AuditWriter audit) : ControllerBase
{
    /// <summary>Exchange email + password for an access / refresh token pair.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokensResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive || !await users.CheckPasswordAsync(user, request.Password))
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials");

        var roles = await users.GetRolesAsync(user);
        var issued = await tokens.IssueAsync(user, roles, ct);
        await audit.WriteAsync("Login", nameof(ApplicationUser), user.Id, ct: ct);
        return new AuthTokensResponse(issued.AccessToken, issued.RefreshToken, issued.ExpiresAtUtc);
    }

    /// <summary>Rotate a refresh token for a fresh access / refresh pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthTokensResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var userId = await tokens.ConsumeRefreshTokenAsync(request.RefreshToken, ct);
        if (userId is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid refresh token");

        var user = await users.FindByIdAsync(userId.Value.ToString());
        if (user is null || !user.IsActive)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account is not active");

        var roles = await users.GetRolesAsync(user);
        var issued = await tokens.IssueAsync(user, roles, ct);
        return new AuthTokensResponse(issued.AccessToken, issued.RefreshToken, issued.ExpiresAtUtc);
    }

    /// <summary>Revoke a refresh token.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await tokens.RevokeRefreshTokenAsync(request.RefreshToken, ct);
        return NoContent();
    }

    /// <summary>The signed-in user, including resolved district / PHC names.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserResponse>> Me(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return Unauthorized();

        var role = currentUser.Roles.FirstOrDefault() ?? Roles.PhcStaff;
        var districtName = user.DistrictId is { } d
            ? await db.Districts.Where(x => x.Id == d).Select(x => x.Name).SingleOrDefaultAsync(ct)
            : null;
        var phcName = user.PhcId is { } p
            ? await db.Phcs.Where(x => x.Id == p).Select(x => x.Name).SingleOrDefaultAsync(ct)
            : null;

        return new CurrentUserResponse(
            user.Id, user.Email ?? "", user.FullName, role,
            user.DistrictId, districtName, user.PhcId, phcName);
    }
}
