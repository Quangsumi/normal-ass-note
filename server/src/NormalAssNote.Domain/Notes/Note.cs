namespace NormalAssNote.Domain.Notes;

public sealed class Note
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = NoteDefaults.Title;
    public string Content { get; set; } = string.Empty;
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
        if (updateContent)
        {
            Content = content ?? string.Empty;
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
}
