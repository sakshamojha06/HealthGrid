using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using HealthGrid.Api.Auth;
using HealthGrid.Api.Data;
using HealthGrid.Api.Hubs;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// -- Configuration ---------------------------------------------------------
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey must be at least 32 characters.")
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// -- Persistence ---------------------------------------------------------
builder.Services.AddDbContext<HealthGridDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("HealthGrid")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<HealthGridDbContext>();

// -- AuthN / AuthZ -----------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role,
        };

        options.Events = new JwtBearerEvents
        {
            // SignalR delivers the token in the query string on the hub handshake.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            // Reject tokens whose security context has changed (deactivated / bumped).
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<HealthGridDbContext>();
                var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                              ?? context.Principal?.FindFirstValue("sub");
                if (!Guid.TryParse(idValue, out var userId))
                {
                    context.Fail("Invalid subject.");
                    return;
                }

                var user = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, u.TokenVersion })
                    .SingleOrDefaultAsync();

                var tokenVersion = context.Principal?.FindFirstValue(ClaimNames.TokenVersion);
                if (user is null || !user.IsActive || tokenVersion != user.TokenVersion.ToString())
                    context.Fail("Token is no longer valid.");
            },
        };
    });

builder.Services.AddAuthorization();

// -- MVC / serialisation --------------------------------------------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Keep nulls so responses match the UI's `T | null` DTO contracts exactly.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddSignalR();
builder.Services.AddHealthChecks().AddDbContextCheck<HealthGridDbContext>();

// -- CORS (dev: Angular served separately without the proxy) --------
const string DevCors = "hg-dev";
builder.Services.AddCors(options => options.AddPolicy(DevCors, policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// -- Application services -------------------------------------------
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuditWriter>();
builder.Services.AddScoped<NotificationDispatcher>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<MedicineRequestService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<VisitService>();
builder.Services.AddScoped<DoctorService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<PredictionService>();

builder.Services.AddHttpClient<AiClient>(client =>
{
    var baseUrl = builder.Configuration["Ai:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue("Ai:TimeoutSeconds", 15));

    var apiKey = builder.Configuration["Ai:ApiKey"];
    if (!string.IsNullOrWhiteSpace(apiKey))
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await DbSeeder.SeedAsync(app.Services);
}

app.UseCors(DevCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.MapHealthChecks("/api/health/ready");

app.Run();
