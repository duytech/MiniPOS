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

    /// <summary>Card surcharge rate in effect when the order was created; charged only when paying by card.</summary>
    public Percentage CardSurchargeRate { get; private set; }

    public PaymentMethod PaymentMethod { get; private set; }

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

    public static Order Create(DateTimeOffset createdAt, Percentage taxRate, Percentage cardSurchargeRate) => new()
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.Open,
        CreatedAt = createdAt.ToUniversalTime(),
        Currency = Currency.USD,
        TaxRate = taxRate,
        CardSurchargeRate = cardSurchargeRate,
        PaymentMethod = PaymentMethod.Cash,
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

    public void SetPaymentMethod(PaymentMethod paymentMethod) => PaymentMethod = paymentMethod;

    /// <summary>
    /// The only place order totals are calculated. Each step is rounded to cents (MidpointRounding.AwayFromZero)
    /// before the next step uses it.
    /// 1. line total = unit price × quantity (exact cents)
    /// 2. subtotal = sum of line totals
    /// 3. discount = percentage of subtotal, or a fixed amount
    /// 4. surcharge = card surcharge rate × (subtotal − discount) when paying by card, otherwise zero
    /// 5. tax = tax rate × (subtotal − discount + surcharge), i.e. the surcharge is taxed
    /// 6. tip = tip rate × (subtotal − discount), i.e. before surcharge and tax
    /// total = subtotal − discount + surcharge + tax + tip
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

        var surchargeBase = discountedSubtotal;
        var surcharge = PaymentMethod == PaymentMethod.Card
            ? PercentOf(surchargeBase, CardSurchargeRate)
            : Money.Zero(Currency);

        var taxBase = discountedSubtotal + surcharge;
        var tax = PercentOf(taxBase, TaxRate);

        var tipBase = discountedSubtotal;
        var tip = TipRate is { } tipRate ? PercentOf(tipBase, tipRate) : Money.Zero(Currency);

        return new OrderTotals(subtotal, discount, surcharge, tax, tip,
            Total: discountedSubtotal + surcharge + tax + tip);
    }

    /// <summary>
    /// Splits the bill by item: each part is a set of whole lines and every line is in exactly one part.
    /// The order's discount, surcharge, tax and tip are each shared by the part's share of the subtotal, rounded down
    /// to the cent; the leftover cents go to the part with the largest subtotal (the earlier part on a tie).
    /// So every amount of the parts adds up exactly to the order's amount, and the part totals to the order total.
    /// </summary>
    public IReadOnlyList<SplitPart> Split(IReadOnlyList<IReadOnlyCollection<Guid>> lineIdsPerPart)
    {
        if (lineIdsPerPart.Count == 0)
        {
            throw new DomainException("A split needs at least one part.");
        }

        var linesById = _lines.ToDictionary(l => l.Id);
        var assignedLineIds = new HashSet<Guid>();
        var partSubtotals = new List<Money>();
        foreach (var lineIds in lineIdsPerPart)
        {
            if (lineIds.Count == 0)
            {
                throw new DomainException("Every part of a split needs at least one line.");
            }

            var partSubtotal = Money.Zero(Currency);
            foreach (var lineId in lineIds)
            {
                if (!linesById.TryGetValue(lineId, out var line))
                {
                    throw new DomainException($"Line {lineId} is not on this order.");
                }

                if (!assignedLineIds.Add(lineId))
                {
                    throw new DomainException($"Line {lineId} is in more than one part.");
                }

                partSubtotal += line.LineTotal;
            }

            partSubtotals.Add(partSubtotal);
        }

        if (assignedLineIds.Count != _lines.Count)
        {
            throw new DomainException("Every line of the order must be in a part of the split.");
        }

        var totals = CalculateTotals();
        var discounts = Allocate(totals.Discount, partSubtotals, totals.Subtotal);
        var surcharges = Allocate(totals.Surcharge, partSubtotals, totals.Subtotal);
        var taxes = Allocate(totals.Tax, partSubtotals, totals.Subtotal);
        var tips = Allocate(totals.Tip, partSubtotals, totals.Subtotal);

        return lineIdsPerPart.Select((lineIds, i) => new SplitPart(
                lineIds.ToList(),
                partSubtotals[i],
                discounts[i],
                surcharges[i],
                taxes[i],
                tips[i],
                Total: partSubtotals[i] - discounts[i] + surcharges[i] + taxes[i] + tips[i]))
            .ToList();
    }

    private Money CalculateSubtotal() => _lines.Aggregate(Money.Zero(Currency), (sum, line) => sum + line.LineTotal);

    private static Money PercentOf(Money amount, Percentage rate) =>
        new(decimal.Round(amount.Amount * rate.Value / 100m, 2, MidpointRounding.AwayFromZero), amount.Currency);

    /// <summary>
    /// Shares <paramref name="amount"/> by each part's share of the subtotal, rounded down to the cent (amounts are never
    /// negative, so ToZero is rounding down); the leftover cents go to the part with the largest subtotal.
    /// </summary>
    private static Money[] Allocate(Money amount, List<Money> partSubtotals, Money subtotal)
    {
        var shares = partSubtotals
            .Select(partSubtotal => subtotal.Amount == 0m
                ? Money.Zero(amount.Currency)
                : new Money(
                    decimal.Round(amount.Amount * partSubtotal.Amount / subtotal.Amount, 2, MidpointRounding.ToZero),
                    amount.Currency))
            .ToArray();

        var leftover = shares.Aggregate(amount, (rest, share) => rest - share);
        var largestPart = 0;
        for (var i = 1; i < partSubtotals.Count; i++)
        {
            if (partSubtotals[i].Amount > partSubtotals[largestPart].Amount)
            {
                largestPart = i;
            }
        }

        shares[largestPart] += leftover;
        return shares;
    }

    private void EnsureSameCurrency(Money money)
    {
        if (money.Currency != Currency)
        {
            throw new DomainException($"The amount is in {money.Currency}, the order is in {Currency}.");
        }
    }
}
