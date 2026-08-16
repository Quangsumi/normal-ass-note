using Microsoft.AspNetCore.Http.Features;
using Microsoft.Net.Http.Headers;
using NormalAssNote.Application.Authentication;
using System.Text;

namespace NormalAssNote.Api.Endpoints;

public static class OidcLogoutEndpoints
{
    private const long MaximumBackChannelBodyBytes = 70 * 1024;

    private const int MaximumLogoutTokenCharacters = 64 * 1024;

    public static IEndpointRouteBuilder MapOidcLogoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/oidc/frontchannel-logout", FrontChannelLogoutAsync).AllowAnonymous();
        app.MapPost("/oidc/backchannel-logout", BackChannelLogoutAsync).AllowAnonymous();

        return app;
    }

    private static async Task<IResult>FrontChannelLogoutAsync(string? iss, string? sid, HttpResponse response, OidcOptions oidcOptions
        , IOidcSessionRevoker sessionRevoker, CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";

        /*
         * Keycloak must be allowed to embed this one response in an iframe.
         * Do not apply X-Frame-Options: DENY/SAMEORIGIN to this endpoint.
         */
        var keycloakOrigin = new Uri(oidcOptions.Authority).GetLeftPart(UriPartial.Authority);
        response.Headers["Content-Security-Policy"] = $"default-src 'none'; " + $"frame-ancestors {keycloakOrigin}"; // Allow only Keycloak to embed this page in an iframe.

        /*
         * We intentionally require both parameters. 
         * In Keycloak UI, configure "Front-channel logout session required".
         */
        if (string.IsNullOrWhiteSpace(iss)
            || string.IsNullOrWhiteSpace(sid)
            || !string.Equals(iss, oidcOptions.Authority, StringComparison.Ordinal)
            || sid.Length > 255)
        {
            return Results.BadRequest();
        }

        await sessionRevoker.RevokeBySessionIdAsync(iss, sid, cancellationToken);

        /*
         * The browser may retain its old opaque cookie, but the referenced
         * PostgreSQL ticket is gone. Its next request is anonymous.
         */
        return Results.Content(
            "<!doctype html><title>Signed out</title>",
            "text/html",
            Encoding.UTF8,
            StatusCodes.Status200OK);
    }

    private static async Task<IResult>BackChannelLogoutAsync(HttpContext httpContext, IOidcLogoutTokenValidator tokenValidator
        , IOidcSessionRevoker sessionRevoker, CancellationToken cancellationToken)
    {
        var request = httpContext.Request;
        var response = httpContext.Response;

        response.Headers.CacheControl = "no-store";

        /*
         * Limit the request before ReadFormAsync consumes the body.
         * The feature can be absent under some test servers.
         */
        var bodySizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

        if (bodySizeFeature is { IsReadOnly: false })
        {
            bodySizeFeature.MaxRequestBodySize = MaximumBackChannelBodyBytes;
        }

        if (request.ContentLength is > MaximumBackChannelBodyBytes)
        {
            return Results.BadRequest();
        }

        // application/x-www-form-urlencoded is for form submissions, which is expected from Keycloak's back-channel logout request.
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var contentType)
            || !string.Equals(contentType.MediaType.Value, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Results.BadRequest();
        }

        var logoutTokenValues = form["logout_token"];

        if (logoutTokenValues.Count != 1)
        {
            return Results.BadRequest();
        }

        var logoutToken = logoutTokenValues[0];

        if (string.IsNullOrWhiteSpace(logoutToken)
            || logoutToken.Length > MaximumLogoutTokenCharacters)
        {
            return Results.BadRequest();
        }

        var validation = await tokenValidator.ValidateAsync(logoutToken, cancellationToken);

        if (validation.Status == OidcLogoutTokenValidationStatus.TemporarilyUnavailable)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (validation.Status != OidcLogoutTokenValidationStatus.Valid
            || validation.Token is null)
        {
            return Results.BadRequest();
        }

        await sessionRevoker.ApplyBackChannelLogoutAsync(validation.Token,cancellationToken);

        /*
         * Applied and AlreadyProcessed both return 200.
         * Do not reveal whether a matching session existed.
         */
        return Results.Ok();
    }
}