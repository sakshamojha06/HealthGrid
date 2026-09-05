using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Controllers;

[ApiController]
public sealed class ErrorController : ControllerBase
{
    [Route("error")]
    public IActionResult Get() => Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred.");
}
