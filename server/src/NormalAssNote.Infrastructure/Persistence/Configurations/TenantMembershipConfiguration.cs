using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NormalAssNote.Domain.Tenants;
using NormalAssNote.Infrastructure.Identity;

namespace NormalAssNote.Infrastructure.Persistence.Configurations;

internal sealed class TenantMembershipConfiguration
    : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> entity)
    {
        entity.ToTable("TenantMemberships", table =>
        {
            table.HasCheckConstraint("CK_TenantMemberships_Role", "\"Role\" IN ('Owner', 'Editor', 'Viewer')");

            table.HasCheckConstraint("CK_TenantMemberships_Status", "\"Status\" IN ('Active', 'Suspended')");

            table.HasCheckConstraint("CK_TenantMemberships_Timestamps", "\"AddedAtUtc\" <= \"UpdatedAtUtc\"");
        });

        entity.HasKey(membership => new
        {
            membership.TenantId,
            membership.ApplicationUserId
        });

        entity.Property(membership => membership.ApplicationUserId)
            .IsRequired();

        entity.Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        entity.Property(membership => membership.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        entity.Property(membership => membership.ConcurrencyStamp)
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        // Used by /api/me to find a user's memberships.
        entity.HasIndex(membership => new
        {
            membership.ApplicationUserId,
            membership.Status
        });

        // Used when listing members or checking active Owners.
        entity.HasIndex(membership => new
        {
            membership.TenantId,
            membership.Status,
            membership.Role
        });

        entity.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Restrict);     // A tenant cannot be deleted while memberships still exist.

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);     // A user cannot be deleted while membership cleanup or ownership transfer is still required.

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull);      // If the user who added a membership is deleted, null out the AddedByUserId.
    }
}