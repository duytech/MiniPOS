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

    /// <summary>Sales tax rate in effect when the order was created; later rate changes don't affect it.</summary>
    public Percentage TaxRate { get; private set; }

    /// <summary>Tip as a percentage of the pre-tax amount (subtotal after discount); null means no tip.</summary>
    public Percentage? TipRate { get; private set; }

    // Stored as two columns; exactly one is set when the order has a discount.
    private Percentage? DiscountRate { get; set; }
    private Money? DiscountAmount { get; set; }

    public OrderDiscount? Discount => (DiscountRate, DiscountAmount) switch
    {
        ({ } rate, null) => OrderDiscount.PercentOfSubtotal(rate),
        (null, { } amount) => OrderDiscount.FixedAmount(amount),
        _ => null,
    };

    public IReadOnlyList<OrderLine> Lines => _lines;

    private Order()
    {
    }

    public static Order Create(DateTimeOffset createdAt, Percentage taxRate) => new()
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.Open,
        CreatedAt = createdAt.ToUniversalTime(),
        Currency = Currency.USD,
        TaxRate = taxRate,
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
    /// Replaces any existing discount. The discount cannot exceed the subtotal at the time it is applied.
    /// </summary>
    public void ApplyDiscount(OrderDiscount discount)
    {
        var subtotal = CalculateSubtotal();
        if (discount.Rate is { Value: > 100m } rate)
        {
            throw new DomainException($"A discount of {rate} is more than the subtotal.");
        }

        if (discount.Amount is { } amount)
        {
            EnsureSameCurrency(amount);
            if (amount.Amount > subtotal.Amount)
            {
                throw new DomainException($"A discount of {amount} is more than the subtotal of {subtotal}.");
            }
        }

        DiscountRate = discount.Rate;
        DiscountAmount = discount.Amount;
    }

    public void RemoveDiscount()
    {
        DiscountRate = null;
        DiscountAmount = null;
    }

    public void SetTip(Percentage tipRate) => TipRate = tipRate;

    public void RemoveTip() => TipRate = null;

    /// <summary>
    /// The only place order totals are calculated. Each step is rounded to cents (MidpointRounding.AwayFromZero)
    /// before the next step uses it.
    /// 1. line total = unit price × quantity (exact cents)
    /// 2. subtotal = sum of line totals
    /// 3. discount = percentage of subtotal, or a fixed amount
    /// 4. surcharge: not part of the order yet; it goes here, after the discount and before tax
    /// 5. tax = tax rate × (subtotal − discount)
    /// 6. tip = tip rate × (subtotal − discount), i.e. on the pre-tax amount
    /// total = subtotal − discount + tax + tip
    /// </summary>
    public OrderTotals CalculateTotals()
    {
        var subtotal = CalculateSubtotal();

        var discount = Discount switch
        {
            { Rate: { } rate } => PercentOf(subtotal, rate),
            { Amount: { } amount } => amount,
            _ => Money.Zero(Currency),
        };
        var discountedSubtotal = subtotal - discount;

        var taxBase = discountedSubtotal;
        var tax = PercentOf(taxBase, TaxRate);

        var tipBase = discountedSubtotal;
        var tip = TipRate is { } tipRate ? PercentOf(tipBase, tipRate) : Money.Zero(Currency);

        return new OrderTotals(subtotal, discount, tax, tip, Total: discountedSubtotal + tax + tip);
    }

    private Money CalculateSubtotal() => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.LineTotal);

    private static Money PercentOf(Money amount, Percentage rate) =>
        new(decimal.Round(amount.Amount * rate.Value / 100m, 2, MidpointRounding.AwayFromZero), amount.Currency);

    private void EnsureSameCurrency(Money money)
    {
        if (money.Currency != Currency)
        {
            throw new DomainException($"The amount is in {money.Currency}, the order is in {Currency}.");
        }
    }
}
