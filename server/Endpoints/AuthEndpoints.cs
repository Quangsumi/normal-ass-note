using Microsoft.AspNetCore.Authentication;
using NormalAssNote.Application.Authentication;
using System.Security.Claims;

namespace NormalAssNote.Api.Endpoints;

public static class AuthEndpoints
{
    private const string AppCookie = "note-cookie";
    private const string KeycloakOidc = "keycloak";
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/login", LoginChallenge).AllowAnonymous();
        app.MapPost("/api/auth/logout", Logout).RequireAuthorization("BrowserCookie");
        app.MapGet("/api/auth/me", MeAsync).RequireAuthorization("BrowserCookie");

        app.MapPost("/api/legacy/auth/login", LegacyLoginAsync).AllowAnonymous();
        app.MapPost("/api/legacy/auth/logout", LegacyLogoutAsync).RequireAuthorization("LegacyBearer");
        app.MapGet("/api/legacy/auth/me", LegacyMeAsync).RequireAuthorization("LegacyBearer");
        app.MapPost("/api/legacy/auth/register", LegacyRegisterAsync).AllowAnonymous();

        return app;
    }

    private static IResult LoginChallenge(string? returnUrl = "/")
    {
        if (string.IsNullOrWhiteSpace(returnUrl)
            || !IsLocalUrl(returnUrl))
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
