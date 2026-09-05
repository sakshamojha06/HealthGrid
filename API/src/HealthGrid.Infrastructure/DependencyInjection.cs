using HealthGrid.Application.Abstractions;
using HealthGrid.Application.Inventory;
using HealthGrid.Infrastructure.Identity;
using HealthGrid.Infrastructure.Inventory;
using HealthGrid.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HealthGrid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<HealthGridDbContext>(options => options.UseSqlServer(configuration.GetConnectionString("HealthGrid")));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<HealthGridDbContext>();
        services.AddHttpContextAccessor();
        services.AddScoped<IUserScope, CurrentUserScope>();
        services.AddScoped<IInventoryService, InventoryService>();
        return services;
    }
}
