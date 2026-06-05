using Microsoft.EntityFrameworkCore;
using NormalAssNote.Application.Notes;
using NormalAssNote.Domain.Notes;
using NormalAssNote.Infrastructure.Persistence;

namespace NormalAssNote.Infrastructure.Notes;

internal sealed class NoteRepository(AppDbContext db) : INoteRepository
{
    public async Task<IReadOnlyList<Note>> ListActiveAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await db.Notes
            .AsNoTracking()
            .Where(note => note.UserId == userId)
            .OrderBy(note => note.SortOrder)
            .ThenBy(note => note.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Note>> ListAllForSyncAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await db.Notes
            .IgnoreQueryFilters()
            .Where(note => note.UserId == userId)
            .ToListAsync(cancellationToken);

    public void Add(Note note) => db.Notes.Add(note);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
