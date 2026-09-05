using HealthGrid.Api.Auth;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HealthGrid.Api.Services;

/// <summary>
/// Turns domain / scope exceptions into consistent RFC-7807 problem responses so
/// controllers stay thin. Unknown exceptions become a 500 without leaking detail.
/// </summary>
public sealed class AppExceptionHandler(
    IProblemDetailsService problems, ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ScopeViolationException or NotFoundError => (StatusCodes.Status404NotFound, "Resource not found"),
            InventoryError or VisitError or DoctorError or MedicineRequestError or PredictionError
                => (StatusCodes.Status422UnprocessableEntity, "Request could not be processed"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
            UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Forbidden"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception");

        httpContext.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status >= 500 ? null : exception.Message,
            },
        });
    }
}
