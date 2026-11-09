namespace Pos.Domain.Orders;

/// <summary>An order item assigned to a part of the saved split.</summary>
public class SavedSplitMenuItem
{
    public Guid OrderMenuItemId { get; set; }

    /// <summary>Index of the item in its part, as it was saved (0 is the first item).</summary>
    public int Position { get; set; }
}
