using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Application.Common;
using System.Text.Json;

namespace NormalAssNote.Infrastructure.Authentication;

/*
 * The validator checks everything required by the Back-Channel Logout specification
 */
internal sealed class OidcLogoutTokenValidator(IOptionsMonitor<OpenIdConnectOptions> openIdConnectOptions, OidcOptions oidcOptions, IClock clock, ILogger<OidcLogoutTokenValidator> logger)
    : IOidcLogoutTokenValidator
{
    private const string KeycloakOidc = "keycloak";

    private const string BackChannelLogoutEvent = "http://schemas.openid.net/event/backchannel-logout";

    // Allow a small amount of clock skew to account for differences between the Keycloak server and the application server.
    private static readonly TimeSpan AllowedClockSkew = TimeSpan.FromMinutes(1); 

    private static readonly TimeSpan MaximumTokenAge = TimeSpan.FromMinutes(5);

    public async Task<OidcLogoutTokenValidationResult> ValidateAsync(string logoutToken, CancellationToken cancellationToken = default)
    {
        try
        {
            var handlerOptions = openIdConnectOptions.Get(KeycloakOidc);

            var configuration = await GetConfigurationAsync(handlerOptions, cancellationToken);

            var validationResult = await ValidateSignatureAndStandardClaimsAsync(handlerOptions, configuration, logoutToken);

            /*
             * Keycloak may have rotated its signing key while ASP.NET still has
             * an older JWKS document cached. Ask for a refresh and retry once.
             */
            if (!validationResult.IsValid 
                && validationResult.Exception is SecurityTokenSignatureKeyNotFoundException 
                && handlerOptions.ConfigurationManager is not null)
            {
                handlerOptions.ConfigurationManager.RequestRefresh();

                configuration = await handlerOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);

                validationResult =await ValidateSignatureAndStandardClaimsAsync(handlerOptions,configuration, logoutToken);
            }

            if (!validationResult.IsValid
                || validationResult.SecurityToken is not JsonWebToken jwt)
            {
                logger.LogWarning("Rejected an OIDC back-channel logout token. " + "Validation failure: {FailureType}.", validationResult.Exception?.GetType().Name ?? "Unknown");

                return OidcLogoutTokenValidationResult.Invalid();
            }

            return ValidateLogoutClaims(jwt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            /*
             * Never include logoutToken in this log entry.
             * A metadata/JWKS failure is different from an invalid token, so the
             * endpoint will return 503 and allow Keycloak to retry.
             */
            logger.LogError(exception, "OIDC metadata or signing keys were unavailable " + "while validating a logout token.");

            return OidcLogoutTokenValidationResult.TemporarilyUnavailable();
        }
    }

    private static async Task<OpenIdConnectConfiguration>GetConfigurationAsync(OpenIdConnectOptions handlerOptions, CancellationToken cancellationToken)
    {
        if (handlerOptions.Configuration is not null)
        {
            return handlerOptions.Configuration;
        }

        if (handlerOptions.ConfigurationManager is null)
        {
            throw new InvalidOperationException("The OIDC configuration manager has not been initialized.");
        }

        return await handlerOptions.ConfigurationManager.GetConfigurationAsync(cancellationToken);
    }

    private async Task<TokenValidationResult>ValidateSignatureAndStandardClaimsAsync(OpenIdConnectOptions handlerOptions, OpenIdConnectConfiguration configuration, string logoutToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            AuthenticationType = KeycloakOidc,

            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = configuration.SigningKeys,

            ValidateIssuer = true,
            ValidIssuer = oidcOptions.Authority,

            ValidateAudience = true,
            ValidAudience = oidcOptions.ClientId,

            /*
             * The Back-Channel Logout specification does not require exp.
             * If exp or nbf is present, ValidateLifetime still validates it.
             * iat freshness is checked separately below.
             */
            ValidateLifetime = true,
            RequireExpirationTime = false,
            ClockSkew = AllowedClockSkew,

            ValidAlgorithms = oidcOptions.AllowedLogoutTokenAlgorithms
        };

        return await handlerOptions.TokenHandler.ValidateTokenAsync(logoutToken, validationParameters);
    }

    private OidcLogoutTokenValidationResult ValidateLogoutClaims(JsonWebToken jwt)
    {
        /*
         * nonce is prohibited in a Logout Token. This helps prevent a Logout
         * Token from being confused with an ID Token.
         */
        if (jwt.TryGetPayloadValue<JsonElement>("nonce", out _))
        {
            return Invalid("nonce is prohibited");
        }

        /*
         * events must be a JSON object containing the standardized
         * back-channel-logout event member.
         */
        if (!jwt.TryGetPayloadValue<JsonElement>("events", out var events) 
            || events.ValueKind != JsonValueKind.Object
            || !events.TryGetProperty(BackChannelLogoutEvent, out var logoutEvent)
            || logoutEvent.ValueKind != JsonValueKind.Object)
        {
            return Invalid("the back-channel logout event is missing");
        }

        if (!TryGetRequiredString(jwt, JwtRegisteredClaimNames.Iss, out var issuer)
            || !string.Equals(issuer, oidcOptions.Authority, StringComparison.Ordinal))
        {
            return Invalid("iss is missing or does not exactly match");
        }

        if (!TryGetRequiredString(jwt, JwtRegisteredClaimNames.Jti, out var jti))
        {
            return Invalid("jti is missing");
        }

        if (!jwt.TryGetPayloadValue<long>(JwtRegisteredClaimNames.Iat, out var iatSeconds))
        {
            return Invalid("iat is missing or is not an integer");
        }

        DateTimeOffset issuedAtUtc;

        try
        {
            issuedAtUtc = DateTimeOffset.FromUnixTimeSeconds(iatSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Invalid("iat is outside the supported range");
        }

        var now = clock.UtcNow;

        if (issuedAtUtc > now + AllowedClockSkew
            || issuedAtUtc < now - MaximumTokenAge - AllowedClockSkew)
        {
            return Invalid("iat is outside the accepted time window");
        }

        var subject = GetOptionalString(jwt, JwtRegisteredClaimNames.Sub);
        var sessionId = GetOptionalString(jwt, JwtRegisteredClaimNames.Sid);

        /*
         * A Logout Token must contain sid or sub, and may contain both.
         */
        if (subject is null && sessionId is null)
        {
            return Invalid("both sub and sid are missing");
        }

        if (subject?.Length > 255 || sessionId?.Length > 255)
        {
            return Invalid("sub or sid exceeds the database limit");
        }

        return OidcLogoutTokenValidationResult.Valid(
            new ValidatedOidcLogoutToken(issuer, jti, subject, sessionId, issuedAtUtc));
    }

    private OidcLogoutTokenValidationResult Invalid(string reason)
    {
        logger.LogWarning("Rejected an OIDC back-channel logout token " + "because {Reason}.", reason);
        return OidcLogoutTokenValidationResult.Invalid();
    }

    private static bool TryGetRequiredString(JsonWebToken jwt, string claimName, out string value)
    {
        if (jwt.TryGetPayloadValue<string>(claimName, out var candidate)
            && !string.IsNullOrWhiteSpace(candidate))
        {
            value = candidate;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? GetOptionalString(JsonWebToken jwt, string claimName)
    {
        return jwt.TryGetPayloadValue<string>(claimName, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    }
}