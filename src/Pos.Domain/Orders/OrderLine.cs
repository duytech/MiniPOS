using Pos.Domain.Common;

namespace Pos.Domain.Orders;

public class OrderLine
{
    public Guid Id { get; private set; }
    public Guid MenuItemId { get; private set; }

    /// <summary>Menu item name at the time the line was added.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Menu item price at the time the line was added; later menu price changes don't affect it.</summary>
    public Money UnitPrice { get; private set; }

    public int Quantity { get; private set; }
    public string? Notes { get; private set; }

    private OrderLine()
    {
    }

    internal OrderLine(Guid menuItemId, string name, Money unitPrice, int quantity, string? notes)
    {
        Id = Guid.NewGuid();
        MenuItemId = menuItemId;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
        Notes = notes;
    }

    /// <summary>Unit price × quantity. Both are exact (whole cents × integer), so no rounding is involved.</summary>
    public Money LineTotal => UnitPrice * Quantity;

    internal bool IsSameAs(Guid menuItemId, Money unitPrice, string? notes) =>
        MenuItemId == menuItemId && UnitPrice == unitPrice && Notes == notes;

    internal void IncreaseQuantity(int quantity)
    {
        try
        {
            Quantity = checked(Quantity + quantity);
        }
        catch (OverflowException)
        {
            throw new DomainException("The line quantity is too large.");
        }
    }
}
