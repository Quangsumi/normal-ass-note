namespace NormalAssNote.Application.Authentication
{
    public static class AppClaimTypes
    {
        /// <summary>
        ///    ClaimsPrincipal
        ///    └── ClaimsIdentity
        ///        ├── iss = https://auth.lab/realms/homelab
        ///        ├── sub = Keycloak user ID
        ///        ├── preferred_username = quang
        ///        ├── sid = Keycloak session ID
        ///        └── app_user_id = local ApplicationUser.Id
        /// </summary>
        public const string UserId = "app_user_id";
    }
}
