namespace Pos.Application.Common;

public interface IClock
{
    DateTime UtcNow { get; }
}
