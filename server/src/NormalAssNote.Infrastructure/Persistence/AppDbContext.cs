using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NormalAssNote.Domain.Notes;
using NormalAssNote.Domain.Tenants;
using NormalAssNote.Infrastructure.Authentication;
using NormalAssNote.Infrastructure.Identity;
using NormalAssNote.Infrastructure.Persistence.Configurations;

namespace NormalAssNote.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options) // add Identity tables (AspNetUsers, AspNetRoles, etc.)
    , IDataProtectionKeyContext // add table DataProtectionKeys
{
    public DbSet<Note> Notes => Set<Note>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    internal DbSet<AuthenticationSession> AuthenticationSessions => Set<AuthenticationSession>();

    /// <summary>
    /// Framework-managed ASP.NET Core Data Protection key ring.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    internal DbSet<OidcLogoutTokenReplay> OidcLogoutTokenReplays => Set<OidcLogoutTokenReplay>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new TenantConfiguration());
        builder.ApplyConfiguration(new TenantMembershipConfiguration());

        builder.Entity<Note>(entity =>
        {
            entity.HasQueryFilter(note => note.DeletedAt == null);
            entity.Property(note => note.Title).HasMaxLength(160);
            entity.Property(note => note.Content).HasColumnType("text");
            entity.HasIndex(note => new { note.UserId, note.DeletedAt, note.SortOrder });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(note => note.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<AuthenticationSession>(entity =>
        {
            entity.ToTable("AuthenticationSessions");

            entity.HasKey(session => session.KeyHash);

            entity.Property(session => session.KeyHash)
                .HasMaxLength(64);

            entity.Property(session => session.AuthenticationScheme)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(session => session.ProtectedTicket)
                .HasColumnType("bytea")
                .IsRequired();

            entity.Property(session => session.Issuer)
                .HasMaxLength(512)
                .IsRequired();

            entity.Property(session => session.Subject)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(session => session.SessionId)
                .HasMaxLength(255);

            entity.Property(session => session.ApplicationUserId)
                .IsRequired();

            // (iss, sid) -> use to revoke one Keycloak login session
            entity.HasIndex(session => new
            {
                session.Issuer,
                session.SessionId
            });

            // (iss, sub)-> use to revoke all app sessions for the Keycloak user
            entity.HasIndex(session => new
            {
                session.Issuer,
                session.Subject
            });

            entity.HasIndex(session => session.ExpiresAtUtc);

            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(session => session.ApplicationUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<OidcLogoutTokenReplay>(entity =>
        {
            entity.ToTable("OidcLogoutTokenReplays");

            entity.HasKey(replay => replay.JtiHash);

            entity.Property(replay => replay.JtiHash)
                .HasMaxLength(64);

            entity.Property(replay => replay.Issuer)
                .HasMaxLength(512)
                .IsRequired();

            entity.HasIndex(replay => replay.ExpiresAtUtc);
        });
    }
}
