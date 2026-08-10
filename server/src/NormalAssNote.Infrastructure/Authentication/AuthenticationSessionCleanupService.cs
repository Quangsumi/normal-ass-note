using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NormalAssNote.Application.Common;
using NormalAssNote.Infrastructure.Persistence;

namespace NormalAssNote.Infrastructure.Authentication;

/*
 A browser stops sending an expired cookie. Therefore, relying only on 
 PostgresTicketStore.RetrieveAsync would leave some expired rows in PostgreSQL forever.
 The delete is idempotent, so that is safe for this application with multiple replicas
 */
internal sealed class AuthenticationSessionCleanupService(IServiceScopeFactory scopeFactory, IClock clock, ILogger<AuthenticationSessionCleanupService> logger) 
    : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DeleteExpiredSessionsAsync(stoppingToken);

        using var timer = new PeriodicTimer(CleanupInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DeleteExpiredSessionsAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private async Task DeleteExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var deletedCount = await db.AuthenticationSessions
                .Where(session => session.ExpiresAtUtc <= clock.UtcNow)
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                logger.LogInformation("Deleted {SessionCount} expired authentication sessions.", deletedCount);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to delete expired authentication sessions.");
        }
    }
}