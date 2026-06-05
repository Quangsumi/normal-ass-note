using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NormalAssNote.Domain.Notes;
using NormalAssNote.Infrastructure.Identity;

namespace NormalAssNote.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

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
    }
}
