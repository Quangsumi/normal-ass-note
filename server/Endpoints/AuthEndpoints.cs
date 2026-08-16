using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using NormalAssNote.Api.Security;
using NormalAssNote.Application.Authentication;
using System.Security.Claims;

namespace NormalAssNote.Api.Endpoints;

public static class AuthEndpoints
{
    private const string AppCookie = "note-cookie";
    private const string KeycloakOidc = "keycloak";
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // logout
        //      POST because GET could triggered by a preview link, external image, ...
        //      RP-initiated: logout both app and keycloak, 

        app.MapGet("/api/auth/session", SessionAsync).AllowAnonymous();
        app.MapGet("/api/auth/login", LoginChallenge).AllowAnonymous();
        app.MapPost("/api/auth/logout", Logout).RequireAuthorization("BrowserCookie").RequireValidAntiforgeryToken();
        app.MapGet("/api/auth/me", MeAsync).RequireAuthorization("BrowserCookie");

        app.MapPost("/api/legacy/auth/login", LegacyLoginAsync).AllowAnonymous();
        app.MapPost("/api/legacy/auth/logout", LegacyLogoutAsync).RequireAuthorization("LegacyBearer");
        app.MapGet("/api/legacy/auth/me", LegacyMeAsync).RequireAuthorization("LegacyBearer");
        app.MapPost("/api/legacy/auth/register", LegacyRegisterAsync).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// React calls this on startup and whenever it wants to revalidate the current browser session.
    /// It intentionally returns 200 for both authenticated and anonymous
    /// sessions. React should inspect the "authenticated" property rather
    /// than treating anonymous startup as an error.
    /// </summary>
    private static async Task<IResult> SessionAsync(ClaimsPrincipal user, HttpContext httpContext, IAntiforgery antiforgery, IWebHostEnvironment environment)
    {
        var tokenSet = antiforgery.GetAndStoreTokens(httpContext);

        var requestToken = tokenSet.RequestToken;

        if (string.IsNullOrWhiteSpace(requestToken))
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Antiforgery token generation failed");
        }

        httpContext.Response.Headers.CacheControl = "no-store, no-cache";
        httpContext.Response.Headers.Pragma = "no-cache";

        var isAuthenticated = user.Identity?.IsAuthenticated == true;

        if (!isAuthenticated)
        {
            return Results.Ok(new
            {
                authenticated = false,
                csrfToken = requestToken
            });
        }

        var applicationUserId = user.FindFirstValue(AppClaimTypes.UserId);

        if (string.IsNullOrWhiteSpace(applicationUserId))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "The authenticated session has no local user mapping");
        }

        var username = user.FindFirstValue("preferred_username") ?? user.Identity?.Name ?? "unknown";
        var displayName = user.FindFirstValue("name") ?? username;

        object? debug = null;

        if (environment.IsDevelopment())
        {
            var cookieAuthentication = await httpContext.AuthenticateAsync(AppCookie);

            debug = new
            {
                issuer = user.FindFirstValue("iss"),
                subject = user.FindFirstValue("sub"),
                sessionId = user.FindFirstValue("sid"),
                appUserId = applicationUserId,
                cookieExpiresAtUtc = cookieAuthentication.Properties?.ExpiresUtc
            };
        }

        return Results.Ok(new
        {
            authenticated = true,
            userName = username,
            displayName,
            email = user.FindFirstValue("email"),
            csrfToken = requestToken,
            debug
        });
    }

    private static IResult LoginChallenge(string? returnUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        return Results.Challenge(
            new AuthenticationProperties
            {
                RedirectUri = returnUrl
            },
            [KeycloakOidc]);
    }

    private static IResult Logout()
    {
        return Results.SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/"
            },
            [AppCookie, KeycloakOidc]);
    }

    private static IResult MeAsync(ClaimsPrincipal user, HttpContext httpContext)
    {
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Ok(new
        {
            appUserId = user.FindFirstValue(AppClaimTypes.UserId),
            issuer = user.FindFirstValue("iss"),
            subject = user.FindFirstValue("sub"),
            sessionId = user.FindFirstValue("sid"),
            username = user.FindFirstValue("preferred_username"),
            name = user.FindFirstValue("name"),
            email = user.FindFirstValue("email")
        });
    }

    #region Legacy Endpoints
    private static async Task<IResult> LegacyRegisterAsync(
        RegisterRequest request,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return ToHttpResult(result);
    }

    private static async Task<IResult> LegacyLoginAsync(
        LoginRequest request,
        IAuthService auth,
        CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return ToHttpResult(result);
    }
    
    private static IResult LegacyLogoutAsync(HttpContext httpContext)
    {
        // For bearer JWTs there is no server-side sign-out to perform. The client
        // should discard the token. Return 200 OK to indicate success.
        return Results.Ok();
    }
    
    private static IResult LegacyMeAsync(ClaimsPrincipal user)
    {
        // Return the same shape for legacy JWT-authenticated clients
        return Results.Ok(new
        {
            subject = user.FindFirst("sub")?.Value,
            username = user.FindFirst("preferred_username")?.Value,
            name = user.FindFirst("name")?.Value,
            email = user.FindFirst("email")?.Value
        });
    }
    #endregion

    private static bool IsLocalUrl(string url)
    {
        // Mimic Url.IsLocalUrl from MVC: local URLs start with '/' but not '//' or '/\\'
        if (string.IsNullOrEmpty(url)) return false;
        if (url[0] == '/')
        {
            if (url.Length == 1) return true;
            return url[1] != '/' && url[1] != '\\';
        }

        // Also allow relative URLs that don't start with '//' or '\\' and do not contain ':'
        if (url[0] == '~') return false;
        return false;
    }
    
    private static IResult ToHttpResult(AuthResult result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(result.Response);
        }

        if (result.Unauthorized)
        {
            return Results.Unauthorized();
        }

        return Results.ValidationProblem(result.Errors);
    }
}
