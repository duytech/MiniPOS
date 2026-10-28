namespace Pos.Domain.Orders;

/// <summary>An order line assigned to a part of the saved split.</summary>
public class SavedSplitLine
{
    public Guid LineId { get; private set; }

    /// <summary>Index of the line in its part, as it was saved (0 is the first line).</summary>
    public int Position { get; private set; }

    private SavedSplitLine()
    {
    }

    internal SavedSplitLine(Guid lineId, int position)
    {
        LineId = lineId;
        Position = position;
    }
}
