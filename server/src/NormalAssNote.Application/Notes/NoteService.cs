using NormalAssNote.Domain.Notes;
using NormalAssNote.Application.Common;

namespace NormalAssNote.Application.Notes;

public sealed class NoteService(INoteRepository notes, IClock clock) : INoteService
{
    public async Task<IReadOnlyList<NoteResponse>> ListAsync(
        string userId,
        string? contentNoteId = null,
        CancellationToken cancellationToken = default)
    {
        var savedNotes = await notes.ListActiveSummariesAsync(userId, cancellationToken);
        if (savedNotes.Count == 0)
        {
            return [];
        }

        var includedContentId = ResolveContentNoteId(savedNotes, contentNoteId);
        var contentNote = await notes.GetActiveAsync(userId, includedContentId, cancellationToken);

        return savedNotes
            .Select(note => new NoteResponse(
                note.Id,
                note.Title,
                note.Id == contentNote?.Id ? contentNote.GetContent() : null,
                note.SortOrder))
            .ToList();
    }

    public async Task<NoteResponse?> GetAsync(
        string userId,
        string noteId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(noteId))
        {
            return null;
        }

        var note = await notes.GetActiveAsync(userId, noteId.Trim(), cancellationToken);
        return note is null ? null : ToResponse(note, includeContent: true);
    }

    public async Task<IReadOnlyList<NoteResponse>> SyncAsync(
        string userId,
        NoteSyncRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var incoming = Normalize(request.Notes);
        var deletedIds = NormalizeDeletedIds(request.DeletedNoteIds);
        var touchedIds = incoming
            .Select(note => note.Id)
            .Concat(deletedIds)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var existing = await notes.ListForSyncAsync(userId, touchedIds, cancellationToken);
        var existingById = existing.ToDictionary(note => note.Id, StringComparer.Ordinal);

        foreach (var noteId in deletedIds)
        {
            if (existingById.TryGetValue(noteId, out var savedNote))
            {
                savedNote.SoftDelete(now);
            }
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

            savedNote.Upsert(
                note.Title,
                note.Content,
                note.SortOrder,
                now,
                note.ContentWasProvided);
        }

        await notes.SaveChangesAsync(cancellationToken);

        return incoming
            .Select(note =>
            {
                var savedNote = existingById[note.Id];
                return ToResponse(savedNote, includeContent: note.ContentWasProvided);
            })
            .ToList();
    }

    private static IReadOnlyList<NormalizedNote> Normalize(IReadOnlyList<NoteInput>? noteInputs) =>
        (noteInputs ?? Array.Empty<NoteInput>())
            .Select((note, index) => new NormalizedNote(
                string.IsNullOrWhiteSpace(note.Id) ? Guid.NewGuid().ToString("N") : note.Id.Trim(),
                string.IsNullOrWhiteSpace(note.Title) ? NoteDefaults.Title : note.Title.Trim(),
                note.Content ?? string.Empty,
                note.Content is not null,
                note.SortOrder ?? index))
            .ToList();

    private static IReadOnlyList<string> NormalizeDeletedIds(IReadOnlyList<string>? noteIds) =>
        (noteIds ?? Array.Empty<string>())
            .Where(noteId => !string.IsNullOrWhiteSpace(noteId))
            .Select(noteId => noteId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string ResolveContentNoteId(
        IReadOnlyList<NoteSummary> savedNotes,
        string? contentNoteId)
    {
        var requestedId = contentNoteId?.Trim();
        if (!string.IsNullOrWhiteSpace(requestedId) &&
            savedNotes.Any(note => note.Id == requestedId))
        {
            return requestedId;
        }

        return savedNotes[0].Id;
    }

    private static NoteResponse ToResponse(Note note, bool includeContent) =>
        new(note.Id, note.Title, includeContent ? note.GetContent() : null, note.SortOrder);

    private sealed record NormalizedNote(
        string Id,
        string Title,
        string Content,
        bool ContentWasProvided,
        int SortOrder);
}
