namespace NormalAssNote.Application.Authentication
{
    public sealed class OidcOptions
    {
        public const string SectionName = "Oidc";

        public string Authority { get; init; } = string.Empty;

        public string ClientId { get; init; } = string.Empty;

        public string ClientSecret { get; init; } = string.Empty;

        /// <summary>
        /// When a valid Keycloak user logs in but the note application cannot find a corresponding local ApplicationUser, should it automatically create one?
        /// false: Do not create a new local user automatically. Reject the login until an administrator explicitly links the Keycloak identity to a local user.
        /// </summary>
        public bool AllowAutomaticUserProvisioning { get; init; }

        /// <summary>
        /// JWS algorithms accepted for OIDC Back-Channel Logout Tokens.
        /// Keycloak uses RS256 by default. Keep this aligned with the
        /// client's configured ID-token signature algorithm.
        /// </summary>
        public string[] AllowedLogoutTokenAlgorithms { get; init; } = ["RS256"];
    }
}
