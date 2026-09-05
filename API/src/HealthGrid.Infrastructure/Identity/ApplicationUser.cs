using Microsoft.AspNetCore.Identity;

namespace HealthGrid.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? DistrictId { get; set; }
    public Guid? PhcId { get; set; }
    public int TokenVersion { get; set; }
}
