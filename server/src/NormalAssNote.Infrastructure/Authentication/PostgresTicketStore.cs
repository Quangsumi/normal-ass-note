using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Application.Common;
using NormalAssNote.Infrastructure.Persistence;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace NormalAssNote.Infrastructure.Authentication;

internal sealed class PostgresTicketStore : ITicketStore
{
    private const string IssuerClaim = "iss";
    private const string SubjectClaim = "sub";
    private const string SessionIdClaim = "sid";

    private static readonly TicketSerializer Serializer = TicketSerializer.Default;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDataProtector _ticketProtector;
    private readonly IClock _clock;
    private readonly ILogger<PostgresTicketStore> _logger;

    public PostgresTicketStore(
        IServiceScopeFactory scopeFactory,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock,
        ILogger<PostgresTicketStore> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;

        // Do not change this purpose string after deployment.
        // Changing it makes all existing stored tickets unreadable.
        _ticketProtector = dataProtectionProvider.CreateProtector("NormalAssNote.AuthenticationSessions.Ticket.v1");
    }

    /*
     * .NET 10 has:
     *
     * - the original ITicketStore methods;
     * - CancellationToken overloads;
     * - HttpContext + CancellationToken overloads.
     *
     * Implementing all overloads ensures cancellation from the current HTTP
     * request reaches PostgreSQL instead of being discarded by the default interface implementations.
     */

    public Task<string> StoreAsync(AuthenticationTicket ticket)
        => StoreCoreAsync(ticket, CancellationToken.None);

    public Task<string> StoreAsync(AuthenticationTicket ticket,CancellationToken cancellationToken)
        => StoreCoreAsync(ticket, cancellationToken);

    public Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext _, CancellationToken cancellationToken)
        => StoreCoreAsync(ticket, cancellationToken);

    public Task RenewAsync(string key, AuthenticationTicket ticket)
        => RenewCoreAsync(key, ticket, CancellationToken.None);

    public Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
        => RenewCoreAsync(key, ticket, cancellationToken);

    public Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext _, CancellationToken cancellationToken)
        => RenewCoreAsync(key, ticket, cancellationToken);

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
        => RetrieveCoreAsync(key, CancellationToken.None);

    public Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken)
        => RetrieveCoreAsync(key, cancellationToken);

    public Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext _, CancellationToken cancellationToken)
        => RetrieveCoreAsync(key, cancellationToken);

    public Task RemoveAsync(string key)
        => RemoveCoreAsync(key, CancellationToken.None);

    public Task RemoveAsync(string key, CancellationToken cancellationToken)
        => RemoveCoreAsync(key, cancellationToken);

    public Task RemoveAsync(string key, HttpContext _, CancellationToken cancellationToken)
        => RemoveCoreAsync(key, cancellationToken);

    private async Task<string> StoreCoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        /*
            1. Generate a cryptographically random raw session key
            2. Hash it with SHA-256
            3. Serialize the complete AuthenticationTicket
            4. Protect the serialized ticket with Data Protection
            5. Insert AuthenticationSessions row
            6. Return the raw session key
            
           The cookie handler then creates a small ticket containing only the raw session key and protects that small ticket into the browser cookie.
           Browser cookie: DataProtection(session-id claim = raw session key)

           Hashing here is similar to storing a password hash. When store, store the hashed version, when retrieve, hash the raw key and compare to the stored hash. 
           This way, if the database is compromised, the attacker cannot use the raw session keys to impersonate users.
        */

        var sessionKey = CreateSessionKey();
        var now = _clock.UtcNow.ToUniversalTime();

        var session = new AuthenticationSession
        {
            KeyHash = HashSessionKey(sessionKey),
            CreatedAtUtc = now
        };

        ApplyTicket(session, ticket, now);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AuthenticationSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        // The random key is returned to CookieAuthenticationHandler and placed inside the Protected cookie
        // PostgreSQL only stores its SHA-256 hash.
        return sessionKey;
    }

    private async Task RenewCoreAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken)
    {
        /*
         The common case is sliding expiration

        Session is halfway through its lifetime
                ↓
        Cookie handler decides it should refresh
                ↓
        RenewAsync(existingRawSessionKey, updatedTicket)
                ↓
        RenewCoreAsync(...)

        1. Hash the existing raw session key
        2. Find the existing database row
        3. Serialize and protect the updated AuthenticationTicket
        4. Update ProtectedTicket
        5. Update issued/expiration timestamps and relevant metadata
        6. Save changes
         */

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var keyHash = HashSessionKey(key);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.AuthenticationSessions.SingleOrDefaultAsync(candidate => candidate.KeyHash == keyHash, cancellationToken);

        if (session is null)
        {
            // Do not resurrect a session that was deleted by logout.
            return;
        }

        ApplyTicket(session, ticket, _clock.UtcNow.ToUniversalTime());

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticationTicket?> RetrieveCoreAsync(string key, CancellationToken cancellationToken)
    {
        /*
            Most frequently used function, called once while authenticating each request containing the OIDC session cookie.

            Request contains cookie
                    ↓
            Cookie handler unprotects cookie
                    ↓
            Extracts raw session key
                    ↓
            RetrieveAsync(rawSessionKey)
                    ↓
            RetrieveCoreAsync(rawSessionKey)

            1. SHA256(raw session key)
            2. Query AuthenticationSessions by KeyHash
            3. Check that the row exists
            4. Check revocation/expiration
            5. Unprotect ProtectedTicket
            6. Deserialize AuthenticationTicket
            7. Return the complete ticket
            8. Cookie handler builds HttpContext.User from it
         */

        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        var keyHash = HashSessionKey(key);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var session = await db.AuthenticationSessions.SingleOrDefaultAsync(candidate => candidate.KeyHash == keyHash, cancellationToken);

        if (session is null)
        {
            return null;
        }

        var now = _clock.UtcNow.ToUniversalTime();

        if (session.ExpiresAtUtc <= now)
        {
            db.AuthenticationSessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        try
        {
            var serializedTicket = _ticketProtector.Unprotect(session.ProtectedTicket);

            return Serializer.Deserialize(serializedTicket);
        }
        catch (Exception exception)
        {
            // Fail closed. Never log ProtectedTicket, OIDC tokens, or the raw key.
            _logger.LogWarning(exception, "An authentication session ticket could not be decrypted or deserialized.");

            return null; // treated as User is unauthenticated
        }
    }

    private async Task RemoveCoreAsync(string key, CancellationToken cancellationToken)
    {
        /*
        User logs out
                ↓
        Cookie handler reads current session key
                ↓
        RemoveAsync(rawSessionKey)
                ↓
        RemoveCoreAsync(...)

        1. Hash the raw session key
        2. Find the row by KeyHash
        3. Delete the row
        4. Save changes
        5. Cookie handler deletes the browser cookie

        Afterward, replaying an old cookie doesn’t work:
            Old cookie
                ↓ raw session key
            SHA256(raw session key)
                ↓
            No AuthenticationSessions row
                ↓
            RetrieveCoreAsync returns null
                ↓
            Unauthenticated
        
        The cookie handler can also call RemoveAsync when it retrieves a ticket and determines that the ticket has expired.
         */

        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var keyHash = HashSessionKey(key);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.AuthenticationSessions
            .Where(session => session.KeyHash == keyHash)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void ApplyTicket(AuthenticationSession session, AuthenticationTicket ticket, DateTimeOffset now)
    {
        var expiresAtUtc = ticket.Properties.ExpiresUtc?.ToUniversalTime()
            ?? throw new InvalidOperationException("The cookie authentication ticket has no expiration.");

        if (string.IsNullOrWhiteSpace(ticket.AuthenticationScheme))
        {
            throw new InvalidOperationException("The authentication ticket has no authentication scheme.");
        }

        session.AuthenticationScheme = ticket.AuthenticationScheme;
        session.Issuer = GetRequiredClaim(ticket, IssuerClaim);
        session.Subject = GetRequiredClaim(ticket, SubjectClaim);
        session.SessionId = GetOptionalClaim(ticket, SessionIdClaim);
        session.ApplicationUserId = GetRequiredClaim(ticket, AppClaimTypes.UserId);

        // ProtectedTicket include ClaimsPrincipal, AuthenticationProperties(ex: id_token, access_token), AuthenticationScheme (eg: Cookies), ...
        session.ProtectedTicket = _ticketProtector.Protect(Serializer.Serialize(ticket));

        session.ExpiresAtUtc = expiresAtUtc;
        session.UpdatedAtUtc = now;
    }

    private static string GetRequiredClaim(AuthenticationTicket ticket, string claimType)
    {
        var value = GetOptionalClaim(ticket, claimType);

        return value
            ?? throw new InvalidOperationException(
                $"The authentication ticket has no '{claimType}' claim.");
    }

    private static string? GetOptionalClaim(AuthenticationTicket ticket, string claimType)
    {
        var value = ticket.Principal.FindFirst(claimType)?.Value;

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string CreateSessionKey()
    {
        // 256 bits of randomness. Base64url produces a cookie-safe value.
        return WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashSessionKey(string sessionKey)
    {
        var keyBytes = Encoding.UTF8.GetBytes(sessionKey);
        return Convert.ToHexString(SHA256.HashData(keyBytes));
    }
}