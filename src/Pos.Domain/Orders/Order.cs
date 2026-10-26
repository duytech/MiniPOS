using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Domain.Orders;

public class Order
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; private set; }

    /// <summary>Sequential number staff use to refer to the order; assigned by the database when the order is saved.</summary>
    public int Number { get; private set; }

    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Currency Currency { get; private set; }
    public IReadOnlyList<OrderLine> Lines => _lines;

    private Order()
    {
    }

    public static Order Create(DateTimeOffset createdAt) => new()
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.Open,
        CreatedAt = createdAt.ToUniversalTime(),
        Currency = Currency.USD,
    };

    /// <summary>
    /// Adds the menu item at its current price. A line with the same menu item, snapshotted unit price and notes
    /// gets its quantity increased; anything different becomes a new line.
    /// </summary>
    public OrderLine AddItem(MenuItem menuItem, int quantity, string? notes)
    {
        if (menuItem.Price.Currency != Currency)
        {
            throw new DomainException($"Menu item is priced in {menuItem.Price.Currency}, the order is in {Currency}.");
        }

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        var line = _lines.FirstOrDefault(l => l.IsSameAs(menuItem.Id, menuItem.Price, normalizedNotes));
        if (line is null)
        {
            line = new OrderLine(menuItem.Id, menuItem.Name, menuItem.Price, quantity, normalizedNotes);
            _lines.Add(line);
        }
        else
        {
            line.IncreaseQuantity(quantity);
        }

        return line;
    }

    /// <summary>
    /// The only place order totals are calculated.
    /// 1. line total = unit price × quantity (exact cents)
    /// 2. subtotal = sum of line totals
    /// Discount, surcharge, tax and tip are not part of the order yet, so total = subtotal. When they are added,
    /// each step goes here, in that order, rounded to cents (MidpointRounding.AwayFromZero) before the next step.
    /// </summary>
    public OrderTotals CalculateTotals()
    {
        var subtotal = _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.LineTotal);
        return new OrderTotals(subtotal, Total: subtotal);
    }
}
