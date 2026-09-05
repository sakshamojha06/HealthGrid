using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Liveness probe — the process is up.</summary>
    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "live", timeUtc = DateTime.UtcNow });
}
