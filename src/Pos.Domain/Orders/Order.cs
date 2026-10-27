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

    /// <summary>When the order was paid; null while it is open or when it was cancelled.</summary>
    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>When the order was cancelled; null while it is open or when it was paid.</summary>
    public DateTimeOffset? CancelledAt { get; private set; }
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
        EnsureOpen();
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
    /// Removes the line from the order; removing the last line leaves the order open and empty. A percent discount
    /// follows the new subtotal. A fixed discount must not be more than the new subtotal: the removal is rejected and
    /// the discount has to be changed or removed first.
    /// </summary>
    public void RemoveLine(OrderLine line)
    {
        EnsureOpen();
        if (!_lines.Contains(line))
        {
            throw new DomainException($"Line {line.Id} is not on this order.");
        }

        EnsureFixedDiscountFits(DiscountAmount, CalculateSubtotal() - line.LineTotal, "Removing the line");
        _lines.Remove(line);
    }

    /// <summary>
    /// Sets the line's quantity to <paramref name="quantity"/>; the same quantity as now leaves the order unchanged.
    /// A percent discount and the tip follow the new subtotal. A fixed discount must not be more than the new
    /// subtotal: the change is rejected and the discount has to be changed or removed first.
    /// </summary>
    public void ChangeLineQuantity(OrderLine line, int quantity)
    {
        EnsureOpen();
        if (!_lines.Contains(line))
        {
            throw new DomainException($"Line {line.Id} is not on this order.");
        }

        if (quantity == line.Quantity)
        {
            return;
        }

        EnsureFixedDiscountFits(
            DiscountAmount,
            CalculateSubtotal() - line.LineTotal + line.UnitPrice * quantity,
            $"A quantity of {quantity}");
        line.ChangeQuantity(quantity);
    }

    /// <summary>
    /// Replaces any existing discount. The discount cannot exceed the subtotal at the time it is applied.
    /// </summary>
    public void ApplyDiscount(OrderDiscount discount)
    {
        EnsureOpen();
        if (discount.Rate is { Value: > 100m } rate)
        {
            throw new DomainException($"A discount of {rate} is more than the subtotal.");
        }

        if (discount.Amount is { } amount)
        {
            EnsureSameCurrency(amount);
        }

        EnsureFixedDiscountFits(discount.Amount, CalculateSubtotal(), change: null);

        DiscountRate = discount.Rate;
        DiscountAmount = discount.Amount;
    }

    public void RemoveDiscount()
    {
        EnsureOpen();
        DiscountRate = null;
        DiscountAmount = null;
    }

    public void SetTip(Percentage tipRate)
    {
        EnsureOpen();
        TipRate = tipRate;
    }

    public void RemoveTip()
    {
        EnsureOpen();
        TipRate = null;
    }

    public void SetPaymentMethod(PaymentMethod paymentMethod)
    {
        EnsureOpen();
        PaymentMethod = paymentMethod;
    }

    /// <summary>
    /// Closes the order as paid, with the stored payment method, for the whole order. Only an open order can be paid
    /// (an empty one too); Paid is final. The totals are not changed.
    /// </summary>
    public void Pay(DateTimeOffset paidAt)
    {
        EnsureOpen();
        Status = OrderStatus.Paid;
        PaidAt = paidAt.ToUniversalTime();
    }

    /// <summary>
    /// Closes the order as cancelled. Only an open order can be cancelled (an empty one too); Cancelled is final.
    /// </summary>
    public void Cancel(DateTimeOffset cancelledAt)
    {
        EnsureOpen();
        Status = OrderStatus.Cancelled;
        CancelledAt = cancelledAt.ToUniversalTime();
    }

    /// <summary>
    /// Items, discount, tip and payment method can only change while the order is open, and only an open order can be
    /// paid or cancelled: Paid and Cancelled are final.
    /// </summary>
    private void EnsureOpen()
    {
        if (Status != OrderStatus.Open)
        {
            throw new DomainException($"The order is {Status}; only an open order can be changed.");
        }
    }

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

    /// <summary>
    /// Splits the bill evenly between <paramref name="parts"/> people. The order is priced first; then its subtotal,
    /// discount, surcharge, tax and tip are each divided by the number of parts, rounded down to the cent, and the
    /// leftover cents of each amount go to the first part. So every amount of the parts adds up exactly to the order's
    /// amount, and the part totals to the order total. Parts hold no lines.
    /// </summary>
    public IReadOnlyList<SplitPart> SplitEvenly(int parts)
    {
        if (parts < 1)
        {
            throw new DomainException("An even split needs at least one part.");
        }

        var totals = CalculateTotals();
        var subtotals = AllocateEvenly(totals.Subtotal, parts);
        var discounts = AllocateEvenly(totals.Discount, parts);
        var surcharges = AllocateEvenly(totals.Surcharge, parts);
        var taxes = AllocateEvenly(totals.Tax, parts);
        var tips = AllocateEvenly(totals.Tip, parts);

        return Enumerable.Range(0, parts)
            .Select(i => new SplitPart(
                [],
                subtotals[i],
                discounts[i],
                surcharges[i],
                taxes[i],
                tips[i],
                Total: subtotals[i] - discounts[i] + surcharges[i] + taxes[i] + tips[i]))
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

    /// <summary>
    /// Divides <paramref name="amount"/> into <paramref name="parts"/> equal shares rounded down to the cent; the
    /// leftover cents go to the first share.
    /// </summary>
    private static Money[] AllocateEvenly(Money amount, int parts)
    {
        var share = new Money(decimal.Round(amount.Amount / parts, 2, MidpointRounding.ToZero), amount.Currency);
        var shares = Enumerable.Repeat(share, parts).ToArray();
        shares[0] = amount - share * (parts - 1);
        return shares;
    }

    /// <summary>
    /// A fixed discount can never be more than the subtotal. <paramref name="change"/> names the line change that would
    /// leave <paramref name="subtotal"/>, or is null when the discount itself is being applied.
    /// </summary>
    private static void EnsureFixedDiscountFits(Money? fixedDiscount, Money subtotal, string? change)
    {
        if (fixedDiscount is not { } discount || discount.Amount <= subtotal.Amount)
        {
            return;
        }

        throw new DomainException(change is null
            ? $"A discount of {discount} is more than the subtotal of {subtotal}."
            : $"{change} would leave a subtotal of {subtotal}, less than the discount of {discount}. " +
              "Change or remove the discount first.");
    }

    private void EnsureSameCurrency(Money money)
    {
        if (money.Currency != Currency)
        {
            throw new DomainException($"The amount is in {money.Currency}, the order is in {Currency}.");
        }
    }
}
