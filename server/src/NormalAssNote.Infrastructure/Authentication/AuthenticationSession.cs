namespace NormalAssNote.Infrastructure.Authentication;

/*
 Browser                                      PostgreSQL
┌──────────────────────────┐       ┌─────────────────────────────┐
│ Protected cookie:        │       │ AuthenticationSessions:     │
│                          │       │                             │
│ raw random session key ──┼──────►│ KeyHash = SHA256(raw key)   │
└──────────────────────────┘       │ ProtectedTicket             │
                                   │ issuer / subject / sid      │
                                   │ expiration                  │
                                   └─────────────────────────────┘
 */

internal sealed class AuthenticationSession
{
    /// <summary>
    /// SHA-256 hash of the random session key returned to the cookie handler.
    /// The raw session key is never stored in PostgreSQL.
    /// HashKey()/KeyHash to find a session <> Data Protection key to encrypts and verifies protected data
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    public string AuthenticationScheme { get; set; } = string.Empty;

    /// <summary>
    /// Data-Protection-protected TicketSerializer output.
    /// This contains the claims, authentication properties, and saved OIDC tokens.
    /// </summary>
    public byte[] ProtectedTicket { get; set; } = [];

    public string Issuer { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak login-session identifier. It may be absent with some providers.
    /// </summary>
    public string? SessionId { get; set; }

    public string ApplicationUserId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }
}