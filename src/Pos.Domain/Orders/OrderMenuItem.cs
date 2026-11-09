using Pos.Domain.Common;

namespace Pos.Domain.Orders;

public class OrderMenuItem
{
    public Guid Id { get; set; }
    public Guid MenuItemId { get; set; }

    /// <summary>Menu item name at the time the item was added.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Menu item price at the time the item was added; later menu price changes don't affect it.</summary>
    public Money UnitPrice { get; set; }

    public int Quantity { get; set; }
    public string? Notes { get; set; }
}
