using System.Security.Claims;

namespace NormalAssNote.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string UserId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user is missing an identifier claim.");
}
