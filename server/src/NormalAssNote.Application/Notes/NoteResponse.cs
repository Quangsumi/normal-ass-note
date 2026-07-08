namespace NormalAssNote.Application.Notes;

public sealed record NoteResponse(string Id, string Title, string? Content, int SortOrder);
