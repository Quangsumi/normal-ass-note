using NormalAssNote.Domain.Notes;
using NormalAssNote.Application.Common;

namespace NormalAssNote.Application.Notes;

public sealed class NoteService(INoteRepository notes, IClock clock) : INoteService
{
    public async Task<IReadOnlyList<NoteResponse>> ListAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var savedNotes = await notes.ListActiveAsync(userId, cancellationToken);
        return savedNotes.Select(ToResponse).ToList();
    }

    public async Task<IReadOnlyList<NoteResponse>> SyncAsync(
        string userId,
        NoteSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await notes.ListAllForSyncAsync(userId, cancellationToken);
        var existingById = existing.ToDictionary(note => note.Id, StringComparer.Ordinal);
        var now = clock.UtcNow;
        var incoming = Normalize(request.Notes);
        var incomingIds = incoming.Select(note => note.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var note in existing.Where(note => !incomingIds.Contains(note.Id)))
        {
            note.SoftDelete(now);
        }

        foreach (var note in incoming)
        {
            if (!existingById.TryGetValue(note.Id, out var savedNote))
            {
                savedNote = new Note
                {
                    Id = note.Id,
                    UserId = userId,
                    CreatedAt = now
                };
                notes.Add(savedNote);
                existingById[note.Id] = savedNote;
            }

            savedNote.Upsert(note.Title, note.Content, note.SortOrder, now);
        }

        await notes.SaveChangesAsync(cancellationToken);

        return incoming
            .Select(note =>
            {
                var savedNote = existingById[note.Id];
                return ToResponse(savedNote);
            })
            .ToList();
    }

    private static IReadOnlyList<NormalizedNote> Normalize(IReadOnlyList<NoteInput>? noteInputs) =>
        (noteInputs ?? Array.Empty<NoteInput>())
            .Select((note, index) => new NormalizedNote(
                string.IsNullOrWhiteSpace(note.Id) ? Guid.NewGuid().ToString("N") : note.Id.Trim(),
                string.IsNullOrWhiteSpace(note.Title) ? NoteDefaults.Title : note.Title.Trim(),
                note.Content ?? string.Empty,
                index))
            .ToList();

    private static NoteResponse ToResponse(Note note) =>
        new(note.Id, note.Title, note.Content, note.SortOrder);

    private sealed record NormalizedNote(string Id, string Title, string Content, int SortOrder);
}
