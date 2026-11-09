namespace Pos.Domain.Orders;

/// <summary>
/// One part of the order's saved split by item: only which items it holds and its position among the parts. Its
/// amounts are not stored; they are recomputed by <see cref="OrderSplitting.Split"/> whenever the split is read.
/// </summary>
public class SavedSplitPart
{
    public Guid Id { get; set; }

    /// <summary>Index of the part in the split (0 is the first part).</summary>
    public int Position { get; set; }

    public List<SavedSplitMenuItem> Items { get; set; } = [];
}
