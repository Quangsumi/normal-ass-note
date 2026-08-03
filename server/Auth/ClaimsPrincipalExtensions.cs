using System.Security.Claims;
using NormalAssNote.Application.Authentication;

namespace NormalAssNote.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal principal)
    {
        // New OIDC application cookie.
        var applicationUserId = principal.FindFirstValue(AppClaimTypes.UserId);

        if (!string.IsNullOrWhiteSpace(applicationUserId))
        {
            return applicationUserId;
        }

        // Temporary compatibility for the old app-issued JWT.
        // Remove this fallback when legacy JWT support is deleted.
        var legacyUserId =
            principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!string.IsNullOrWhiteSpace(legacyUserId))
        {
            return legacyUserId;
        }

        throw new InvalidOperationException(
            "The authenticated identity is missing its local ApplicationUser identifier.");
    }
}