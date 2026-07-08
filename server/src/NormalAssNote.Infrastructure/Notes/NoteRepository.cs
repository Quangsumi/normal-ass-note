using Microsoft.EntityFrameworkCore;
using NormalAssNote.Application.Notes;
using NormalAssNote.Domain.Notes;
using NormalAssNote.Infrastructure.Persistence;

namespace NormalAssNote.Infrastructure.Notes;

internal sealed class NoteRepository(AppDbContext db) : INoteRepository
{
    public async Task<IReadOnlyList<NoteSummary>> ListActiveSummariesAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        await db.Notes
            .AsNoTracking()
            .Where(note => note.UserId == userId)
            .OrderBy(note => note.SortOrder)
            .ThenBy(note => note.CreatedAt)
            .Select(note => new NoteSummary(
                note.Id,
                note.Title,
                note.SortOrder,
                note.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<Note?> GetActiveAsync(
        string userId,
        string noteId,
        CancellationToken cancellationToken = default) =>
        await db.Notes
            .AsNoTracking()
            .Where(note => note.UserId == userId && note.Id == noteId)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Note>> ListForSyncAsync(
        string userId,
        IReadOnlyCollection<string> noteIds,
        CancellationToken cancellationToken = default)
    {
        if (noteIds.Count == 0)
        {
            return [];
        }

        return await db.Notes
            .IgnoreQueryFilters()
            .Where(note => note.UserId == userId && noteIds.Contains(note.Id))
            .ToListAsync(cancellationToken);
    }

    public void Add(Note note) => db.Notes.Add(note);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
