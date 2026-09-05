using HealthGrid.Api.Contracts;
using HealthGrid.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    /// <summary>One scoped operational snapshot for the landing dashboard.</summary>
    [HttpGet]
    public Task<DashboardSummaryDto> Get(CancellationToken ct) => dashboard.BuildAsync(ct);
}
