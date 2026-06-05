namespace NormalAssNote.Application.Common;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
