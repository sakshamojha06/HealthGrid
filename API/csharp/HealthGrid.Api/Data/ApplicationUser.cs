using Microsoft.AspNetCore.Identity;

namespace HealthGrid.Api.Data;

/// <summary>
/// ASP.NET Core Identity user. District / PHC membership lives here and is copied
/// into the JWT — the API never trusts a district or PHC id supplied by the client.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public required string FullName { get; set; }
    public Guid? DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Bumped to invalidate every outstanding token for this user.</summary>
    public int TokenVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
