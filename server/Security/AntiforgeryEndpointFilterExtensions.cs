using Microsoft.AspNetCore.Antiforgery;

namespace NormalAssNote.Api.Security;

public static class AntiforgeryEndpointFilterExtensions
{
    /// <summary>
    /// Validates the ASP.NET antiforgery cookie/request-token pair before
    /// allowing a state-changing Minimal API endpoint to execute.
    /// </summary>
    public static RouteHandlerBuilder RequireValidAntiforgeryToken(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilter(async (invocationContext, next) =>
            {
                var httpContext = invocationContext.HttpContext;

                var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();

                /*
                 Note synchronization endpoint accepts JSON, not form-bound data. 
                 ASP.NET's automatic Minimal API antiforgery behavior is primarily driven by endpoint metadata and Form binding. 
                 Calling IAntiforgery.IsRequestValidAsync directly gives this application deterministic behavior.
                 */
                var isValid = await antiforgery.IsRequestValidAsync(httpContext);

                if (isValid)
                {
                    return await next(invocationContext);
                }

                var logger = httpContext.RequestServices
                                .GetRequiredService<ILoggerFactory>()
                                .CreateLogger("NormalAssNote.Antiforgery");

                // Never log either antiforgery token.
                logger.LogWarning(
                    "Rejected request with an invalid antiforgery token. Method {Method}, path {Path}.",
                    httpContext.Request.Method,
                    httpContext.Request.Path);

                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid antiforgery token",
                    detail: "Refresh the application session and retry the request.");
            });
    }
}