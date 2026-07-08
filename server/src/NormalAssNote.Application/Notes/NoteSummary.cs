namespace NormalAssNote.Application.Notes;

public sealed record NoteSummary(
    string Id,
    string Title,
    int SortOrder,
    DateTimeOffset CreatedAt);
