using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>
/// A line on an order. Name and unit price are snapshotted from the menu item
/// at the time it is added, so later menu price changes do not alter the order.
/// </summary>
public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    // For EF Core.
    private OrderItem() { }

    internal OrderItem(Guid orderId, Guid menuItemId, string name, decimal unitPrice, int quantity)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        MenuItemId = menuItemId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");
        Quantity = checked(Quantity + quantity);
    }
}
