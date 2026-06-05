using NormalAssNote.Domain.Notes;

namespace NormalAssNote.Application.Notes;

public interface INoteRepository
{
    Task<IReadOnlyList<Note>> ListActiveAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Note>> ListAllForSyncAsync(string userId, CancellationToken cancellationToken = default);
    void Add(Note note);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
