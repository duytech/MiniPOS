namespace Pos.Domain.Orders;

/// <summary>
/// One part of the order's saved split by item: only which lines it holds and its position among the parts. Its
/// amounts are not stored; they are recomputed by <see cref="Order.Split"/> whenever the split is read.
/// </summary>
public class SavedSplitPart
{
    private readonly List<SavedSplitLine> _lines = [];

    public Guid Id { get; private set; }

    /// <summary>Index of the part in the split (0 is the first part).</summary>
    public int Position { get; private set; }

    public IReadOnlyList<SavedSplitLine> Lines => _lines;

    private SavedSplitPart()
    {
    }

    internal SavedSplitPart(int position, IEnumerable<Guid> lineIds)
    {
        Id = Guid.NewGuid();
        Position = position;
        _lines.AddRange(lineIds.Select(lineId => new SavedSplitLine(lineId)));
    }
}
