using NormalAssNote.Application.Common;

namespace NormalAssNote.Infrastructure.Common;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
