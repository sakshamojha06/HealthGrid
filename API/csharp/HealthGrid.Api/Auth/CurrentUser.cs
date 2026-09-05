using System.Security.Claims;

namespace HealthGrid.Api.Auth;

/// <summary>
/// The authenticated caller's identity and data scope, resolved from JWT claims.
/// District isolation is built on top of this: application code must filter every
/// query by <see cref="DistrictId"/> unless <see cref="IsSystemAdministrator"/>.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    string? Email { get; }
    IReadOnlyList<string> Roles { get; }
    Guid? DistrictId { get; }
    Guid? PhcId { get; }

    bool IsSystemAdministrator { get; }
    bool IsInRole(string role);

    /// <summary>True when the caller may read/write data for the given district (and PHC).</summary>
    bool CanAccess(Guid districtId, Guid? phcId = null);

    /// <summary>Throws <see cref="ScopeViolationException"/> when the target is out of scope.</summary>
    void EnsureAccess(Guid districtId, Guid? phcId = null);
}

/// <summary>Thrown when a request targets data outside the caller's district / PHC scope.</summary>
public sealed class ScopeViolationException(string message = "Resource is outside your scope.")
    : Exception(message);

/// <summary>Thrown when a resource does not exist (or is hidden because it is out of scope).</summary>
public sealed class NotFoundError(string message = "Resource not found.") : Exception(message);

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(
        Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Principal?.FindFirstValue("sub"), out var id)
        ? id
        : Guid.Empty;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue("email");

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray() ?? [];

    public Guid? DistrictId => ParseGuid(Principal?.FindFirstValue(ClaimNames.DistrictId));
    public Guid? PhcId => ParseGuid(Principal?.FindFirstValue(ClaimNames.PhcId));

    public bool IsSystemAdministrator => IsInRole(Auth.Roles.SystemAdministrator);

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool CanAccess(Guid districtId, Guid? phcId = null)
    {
        if (IsSystemAdministrator)
            return true;
        if (DistrictId is null || DistrictId != districtId)
            return false;

        // District administrators see the whole district; PHC-bound roles see only their PHC.
        if (IsInRole(Auth.Roles.DistrictAdministrator))
            return true;

        return PhcId is null || phcId is null || PhcId == phcId;
    }

    public void EnsureAccess(Guid districtId, Guid? phcId = null)
    {
        if (!CanAccess(districtId, phcId))
            throw new ScopeViolationException();
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var g) ? g : null;
}
