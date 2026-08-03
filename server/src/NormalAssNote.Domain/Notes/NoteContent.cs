namespace NormalAssNote.Domain.Notes;

public sealed class NoteContent
{
    public string NoteId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Note? Note { get; set; }
}
