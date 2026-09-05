using System.Security.Claims;
using HealthGrid.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HealthGrid.Infrastructure.Identity;

public sealed class CurrentUserScope(IHttpContextAccessor httpContextAccessor) : IUserScope
{
    public UserScope Current
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            var district = ParseGuid(user?.FindFirstValue("district_id"));
            var phc = ParseGuid(user?.FindFirstValue("phc_id"));
            return new UserScope(district, phc, user?.IsInRole("SystemAdministrator") == true);
        }
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var result) ? result : null;
}
