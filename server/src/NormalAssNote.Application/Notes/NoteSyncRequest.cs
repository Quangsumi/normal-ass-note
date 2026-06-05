namespace NormalAssNote.Application.Notes;

public sealed record NoteSyncRequest(IReadOnlyList<NoteInput>? Notes);
