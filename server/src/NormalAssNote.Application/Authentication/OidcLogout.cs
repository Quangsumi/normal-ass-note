namespace NormalAssNote.Application.Authentication;

public sealed record ValidatedOidcLogoutToken(string Issuer, string Jti, string? Subject, string? SessionId, DateTimeOffset IssuedAtUtc);

public enum OidcLogoutTokenValidationStatus
{
    Valid,
    Invalid,
    TemporarilyUnavailable
}

public sealed record OidcLogoutTokenValidationResult(OidcLogoutTokenValidationStatus Status, ValidatedOidcLogoutToken? Token)
{
    public static OidcLogoutTokenValidationResult Valid(ValidatedOidcLogoutToken token) 
        => new(OidcLogoutTokenValidationStatus.Valid, token);

    public static OidcLogoutTokenValidationResult Invalid() 
        => new(OidcLogoutTokenValidationStatus.Invalid, null);

    public static OidcLogoutTokenValidationResult TemporarilyUnavailable() 
        => new(OidcLogoutTokenValidationStatus.TemporarilyUnavailable, null);
}

public interface IOidcLogoutTokenValidator
{
    Task<OidcLogoutTokenValidationResult> ValidateAsync(string logoutToken, CancellationToken cancellationToken = default);
}

public enum OidcLogoutApplicationResult
{
    Applied,
    AlreadyProcessed
}

public interface IOidcSessionRevoker
{
    /*
     * use for front-channel logout
     */
    Task<int> RevokeBySessionIdAsync(string issuer, string sessionId, CancellationToken cancellationToken = default);

    /*
     * use for back-channel logout
     */
    Task<OidcLogoutApplicationResult> ApplyBackChannelLogoutAsync(ValidatedOidcLogoutToken logoutToken, CancellationToken cancellationToken = default);
}