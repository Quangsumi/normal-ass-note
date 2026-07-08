namespace NormalAssNote.Application.Notes;

public interface INoteService
{
    Task<IReadOnlyList<NoteResponse>> ListAsync(
        string userId,
        string? contentNoteId = null,
        CancellationToken cancellationToken = default);
    Task<NoteResponse?> GetAsync(
        string userId,
        string noteId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteResponse>> SyncAsync(string userId, NoteSyncRequest request, CancellationToken cancellationToken = default);
}
