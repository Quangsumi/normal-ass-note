namespace NormalAssNote.Domain.Notes;

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = NoteDefaults.Title;
    public NoteContent? Content { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public void Upsert(
        string title,
        string? content,
        int sortOrder,
        DateTimeOffset now,
        bool updateContent)
    {
        Title = string.IsNullOrWhiteSpace(title) ? NoteDefaults.Title : title.Trim();
        EnsureContent();
        if (updateContent)
        {
            Content!.Value = content ?? string.Empty;
        }

        SortOrder = sortOrder;
        UpdatedAt = now;
        DeletedAt = null;
    }

    public void SoftDelete(DateTimeOffset now)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = now;
        UpdatedAt = now;
    }

    public string GetContent() => Content?.Value ?? string.Empty;

    private void EnsureContent()
    {
        if (Content is null)
        {
            Content = new NoteContent
            {
                NoteId = Id
            };
        }
    }
}
