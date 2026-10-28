namespace Pos.Domain.Orders;

/// <summary>An order line assigned to a part of the saved split.</summary>
public class SavedSplitLine
{
    public Guid LineId { get; private set; }

    private SavedSplitLine()
    {
    }

    internal SavedSplitLine(Guid lineId)
    {
        LineId = lineId;
    }
}
