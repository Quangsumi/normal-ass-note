namespace NormalAssNote.Application.Notes;

public interface INoteService
{
    Task<IReadOnlyList<NoteResponse>> ListAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteResponse>> SyncAsync(string userId, NoteSyncRequest request, CancellationToken cancellationToken = default);
}
