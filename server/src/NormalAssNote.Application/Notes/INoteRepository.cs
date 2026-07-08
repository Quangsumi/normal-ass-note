using NormalAssNote.Domain.Notes;

namespace NormalAssNote.Application.Notes;

public interface INoteRepository
{
    Task<IReadOnlyList<NoteSummary>> ListActiveSummariesAsync(
        string userId,
        CancellationToken cancellationToken = default);
    Task<Note?> GetActiveAsync(
        string userId,
        string noteId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> ListForSyncAsync(
        string userId,
        IReadOnlyCollection<string> noteIds,
        CancellationToken cancellationToken = default);
    void Add(Note note);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
