namespace NormalAssNote.Infrastructure.Authentication;

internal sealed class OidcLogoutTokenReplay
{
    /// <summary>
    /// unique identifier of the Back-Channel Logout Token. 
    /// Storing a hash of it makes processing idempotent and prevents the same valid token from repeatedly triggering work.
    /// SHA-256 of issuer + NUL + jti.
    /// The raw jti is not persisted.
    /// </summary>
    public string JtiHash { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public DateTimeOffset ReceivedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}