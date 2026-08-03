using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Infrastructure.Authentication;

namespace NormalAssNote.Infrastructure.Identity;

internal sealed class OidcUserProvisioner(UserManager<ApplicationUser> userManager, OidcOptions oidcOptions, ILogger<OidcUserProvisioner> logger)
{
    public async Task<ApplicationUser> ResolveAsync(ClaimsPrincipal principal)
    {
        var issuer = GetRequiredClaim(principal, "iss");
        var subject = GetRequiredClaim(principal, "sub");

        // The OIDC middleware already performs issuer validation against
        // discovery metadata. This explicit comparison prevents accidentally
        // storing a mapping under an unexpected issuer configuration.
        if (!string.Equals(issuer, oidcOptions.Authority, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The validated token issuer does not match Oidc:Authority.");
        }

        // Searches AspNetUserLogins by issuer and subject, then returns the associated user from AspNetUsers
        // LoginProvider = issuer (https://auth.lab/realms/homelab)
        // ProviderKey   = subject (keycloak user id)
        var existingUser = await userManager.FindByLoginAsync(issuer, subject);

        if (existingUser is not null)
        {
            return existingUser;
        }

        if (!oidcOptions.AllowAutomaticUserProvisioning)
        {
            // Exact values are useful during the one-time linking process.
            // Avoid this detailed logging outside your controlled environment.
            logger.LogWarning("No local ApplicationUser mapping exists for OIDC issuer {Issuer} and subject {Subject}.", issuer, subject);

            throw new InvalidOperationException("This Keycloak account has not been linked to a local note account.");
        }

        return await CreateAndLinkUserAsync(issuer, subject);
    }

    private async Task<ApplicationUser> CreateAndLinkUserAsync(string issuer, string subject)
    {
        // 1. CreateAsync(user): Create local user in table AspNetUsers
        // 2. AddLoginAsync(user, loginInfo): link Keycloak identity to that local user, add to table AspNetUserLogins

        // This name is internal only. Do not use preferred_username or email as the database identity because both can change.
        var user = new ApplicationUser
        {
            UserName = $"oidc_{Guid.NewGuid():N}" // :N formats the GUID as 32 digits without hyphens.
        };

        // No password is supplied. Keycloak owns this user's credentials.
        var createResult = await userManager.CreateAsync(user);

        if (!createResult.Succeeded)
        {
            throw IdentityFailure("Creating the local ApplicationUser failed", createResult);
        }

        var loginInfo = new UserLoginInfo(
            loginProvider: issuer,
            providerKey: subject,
            providerDisplayName: "Keycloak");

        var linkResult = await userManager.AddLoginAsync(user, loginInfo);

        if (linkResult.Succeeded)
        {
            logger.LogInformation("Created local ApplicationUser {ApplicationUserId} for an OIDC identity.",user.Id);
            return user;
        }

        // A second callback for the same identity could race with this one.
        // If another request successfully created the external-login mapping,
        // use that winning local user and remove our unused user.
        var winningUser = await userManager.FindByLoginAsync(issuer, subject);

        if (winningUser is not null)
        {
            await userManager.DeleteAsync(user);
            return winningUser;
        }

        // Avoid leaving an unlinked local user after a normal failure.
        await userManager.DeleteAsync(user);

        throw IdentityFailure("Linking the OIDC identity to the local ApplicationUser failed", linkResult);
    }

    private static string GetRequiredClaim(
        ClaimsPrincipal principal,
        string claimType)
    {
        var value = principal.FindFirstValue(claimType);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"The validated OIDC identity is missing the '{claimType}' claim.");
        }

        return value;
    }

    private static InvalidOperationException IdentityFailure(string operation, IdentityResult result)
    {
        var errors = string.Join(
            "; ",
            result.Errors.Select(error =>
                $"{error.Code}: {error.Description}"));

        return new InvalidOperationException(
            $"{operation}. {errors}");
    }
}