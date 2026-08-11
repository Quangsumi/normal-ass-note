using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NormalAssNote.Application.Authentication;
using NormalAssNote.Application.Common;
using NormalAssNote.Infrastructure.Persistence;
using System.Security.Cryptography;
using System.Text;

namespace NormalAssNote.Infrastructure.Authentication;

internal sealed class OidcSessionRevoker(AppDbContext db, IClock clock, ILogger<OidcSessionRevoker> logger)
    : IOidcSessionRevoker
{
    private static readonly TimeSpan ReplayRetention = TimeSpan.FromHours(24);

    /*
     * use for front-channel logout
     */
    public async Task<int> RevokeBySessionIdAsync(string issuer, string sessionId, CancellationToken cancellationToken = default)
    {
        var deletedCount = await db.AuthenticationSessions
            .Where(session => session.Issuer == issuer && session.SessionId == sessionId)
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Applied OIDC front-channel logout for issuer " + "{Issuer}; revoked {SessionCount} session(s).", issuer, deletedCount);

        return deletedCount;
    }

    /*
     * use for back-channel logout
     */
    public async Task<OidcLogoutApplicationResult>ApplyBackChannelLogoutAsync(ValidatedOidcLogoutToken logoutToken, CancellationToken cancellationToken = default)
    {
        /*
            sid present: revoke that one Keycloak login session.
            sid absent, sub present: revoke every note-app session belonging to that Keycloak user.
            A valid token that matches zero rows still succeeds. Logout is idempotent.
            A repeated valid jti also returns success without processing again.
         */

        var now = clock.UtcNow;

        var jtiHash = HashJti(logoutToken.Issuer, logoutToken.Jti);

        /*
         * Bug if not atomic:
            1. Insert jti replay record — committed
            2. Delete authentication session — fails
            3. Keycloak retries the same token
            4. Application sees existing jti and skips it
            5. Authentication session remains active
          
         * Atomic prevent bug:
            1. Insert replay record
            2. Delete session fails
            3. Entire transaction rolls back
            4. Replay record is not retained
            5. Keycloak retry can process the token again
         */

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        /*
         * ON CONFLICT makes replay detection atomic when multiple replicas receive the same Logout Token concurrently.
         * ExecuteSqlInterpolatedAsync safe parameterization, ExecuteSqlRaw is not safe
         */
        var insertedCount = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "OidcLogoutTokenReplays"
                    ("JtiHash",
                     "Issuer",
                     "ReceivedAtUtc",
                     "ExpiresAtUtc")
                VALUES
                    ({jtiHash},
                     {logoutToken.Issuer},
                     {now},
                     {now + ReplayRetention})
                ON CONFLICT ("JtiHash") DO NOTHING
                """, cancellationToken);

        if (insertedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            logger.LogInformation("Ignored an already-processed OIDC " + "back-channel logout token for issuer {Issuer}.", logoutToken.Issuer);

            return OidcLogoutApplicationResult.AlreadyProcessed;
        }

        IQueryable<AuthenticationSession> sessions = db.AuthenticationSessions.Where(session => session.Issuer == logoutToken.Issuer);

        if (logoutToken.SessionId is not null)
        {
            /*
             * sid identifies one OP login session.
             * If sub is also supplied, require both values to match.
             */
            sessions = sessions.Where(session => session.SessionId == logoutToken.SessionId);

            if (logoutToken.Subject is not null)
            {
                sessions = sessions.Where(session => session.Subject == logoutToken.Subject);
            }
        }
        else
        {
            /*
             * If sid is absent, the specification says the intent is to
             * terminate all RP sessions for this issuer + subject.
             */
            sessions = sessions.Where(session => session.Subject == logoutToken.Subject!);
        }

        var deletedCount = await sessions.ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Applied OIDC back-channel logout for issuer " + "{Issuer}; revoked {SessionCount} session(s).", logoutToken.Issuer, deletedCount);

        return OidcLogoutApplicationResult.Applied;
    }

    private static string HashJti(string issuer, string jti)
    {
        var bytes = Encoding.UTF8.GetBytes($"{issuer}\0{jti}");

        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}