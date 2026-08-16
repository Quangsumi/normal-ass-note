using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NormalAssNote.Domain.Tenants;
using NormalAssNote.Infrastructure.Identity;

namespace NormalAssNote.Infrastructure.Persistence.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> entity)
    {
        entity.ToTable("Tenants", table =>
        {
            table.HasCheckConstraint("CK_Tenants_Slug_Format", "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");

            table.HasCheckConstraint("CK_Tenants_Name_NotBlank", "length(btrim(\"Name\")) > 0");

            table.HasCheckConstraint("CK_Tenants_Status", "\"Status\" IN ('Active', 'Archived')");

            table.HasCheckConstraint("CK_Tenants_Timestamps", "\"CreatedAtUtc\" <= \"UpdatedAtUtc\"");
        });

        entity.HasKey(tenant => tenant.Id);

        entity.Property(tenant => tenant.Id)
            .ValueGeneratedNever();

        entity.Property(tenant => tenant.Slug)
            .HasMaxLength(64)
            .IsRequired();

        entity.Property(tenant => tenant.Name)
            .HasMaxLength(120)
            .IsRequired();

        entity.Property(tenant => tenant.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        entity.Property(tenant => tenant.ConcurrencyStamp)
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .IsRequired();

        entity.HasIndex(tenant => tenant.Slug)
            .IsUnique();

        entity.HasIndex(tenant => tenant.Status);

        entity.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(tenant => tenant.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}