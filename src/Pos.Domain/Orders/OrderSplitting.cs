using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>Prices a split of the bill from the order's totals (<see cref="OrderPricing.CalculateTotals"/>).</summary>
public static class OrderSplitting
{
    /// <summary>
    /// Splits the bill by item: each part is a set of whole lines and every line is in exactly one part.
    /// The order's discount, surcharge, tax and tip are each shared by the part's share of the subtotal, rounded down
    /// to the cent; the leftover cents go to the part with the largest subtotal (the earlier part on a tie).
    /// So every amount of the parts adds up exactly to the order's amount, and the part totals to the order total.
    /// A split needs at least two parts. Nothing is stored and any status is allowed.
    /// </summary>
    public static Result<IReadOnlyList<SplitPart>> Split(Order order, IReadOnlyList<IReadOnlyCollection<Guid>> lineIdsPerPart)
    {
        if (lineIdsPerPart.Count < 2)
        {
            return InvalidSplit("A split needs at least two parts.");
        }

        var linesById = order.Lines.ToDictionary(l => l.Id);
        var assignedLineIds = new HashSet<Guid>();
        var partSubtotals = new List<Money>();
        foreach (var lineIds in lineIdsPerPart)
        {
            if (lineIds.Count == 0)
            {
                return InvalidSplit("Every part of a split needs at least one line.");
            }

            var partSubtotal = Money.Zero(order.Currency);
            foreach (var lineId in lineIds)
            {
                if (!linesById.TryGetValue(lineId, out var line))
                {
                    return Result.Fail<IReadOnlyList<SplitPart>>(OrderRules.LineNotOnOrder(lineId));
                }

                if (!assignedLineIds.Add(lineId))
                {
                    return InvalidSplit($"Line {lineId} is in more than one part.");
                }

                partSubtotal += OrderPricing.LineTotal(line);
            }

            partSubtotals.Add(partSubtotal);
        }

        if (assignedLineIds.Count != order.Lines.Count)
        {
            return InvalidSplit("Every line of the order must be in a part of the split.");
        }

        var totals = OrderPricing.CalculateTotals(order);
        var discounts = Allocate(totals.Discount, partSubtotals, totals.Subtotal);
        var surcharges = Allocate(totals.Surcharge, partSubtotals, totals.Subtotal);
        var taxes = Allocate(totals.Tax, partSubtotals, totals.Subtotal);
        var tips = Allocate(totals.Tip, partSubtotals, totals.Subtotal);

        return Result.Ok<IReadOnlyList<SplitPart>>(lineIdsPerPart.Select((lineIds, i) => new SplitPart(
                lineIds.ToList(),
                partSubtotals[i],
                discounts[i],
                surcharges[i],
                taxes[i],
                tips[i],
                Total: partSubtotals[i] - discounts[i] + surcharges[i] + taxes[i] + tips[i]))
            .ToList());
    }

    /// <summary>The line ids of the order's saved split, per part, in saved order.</summary>
    public static IReadOnlyList<IReadOnlyCollection<Guid>> SavedSplitLineIds(Order order) => order.SavedSplitParts
        .OrderBy(p => p.Position)
        .Select(p => (IReadOnlyCollection<Guid>)p.Lines.OrderBy(l => l.Position).Select(l => l.LineId).ToList())
        .ToList();

    /// <summary>
    /// Splits the bill evenly between <paramref name="parts"/> people. The order is priced first; then its subtotal,
    /// discount, surcharge, tax and tip are each divided by the number of parts, rounded down to the cent, and the
    /// leftover cents of each amount go to the first part. So every amount of the parts adds up exactly to the order's
    /// amount, and the part totals to the order total. Parts hold no lines.
    /// </summary>
    public static Result<IReadOnlyList<SplitPart>> SplitEvenly(Order order, int parts)
    {
        if (parts < 1)
        {
            return InvalidSplit("An even split needs at least one part.");
        }

        var totals = OrderPricing.CalculateTotals(order);
        var subtotals = AllocateEvenly(totals.Subtotal, parts);
        var discounts = AllocateEvenly(totals.Discount, parts);
        var surcharges = AllocateEvenly(totals.Surcharge, parts);
        var taxes = AllocateEvenly(totals.Tax, parts);
        var tips = AllocateEvenly(totals.Tip, parts);

        return Result.Ok<IReadOnlyList<SplitPart>>(Enumerable.Range(0, parts)
            .Select(i => new SplitPart(
                [],
                subtotals[i],
                discounts[i],
                surcharges[i],
                taxes[i],
                tips[i],
                Total: subtotals[i] - discounts[i] + surcharges[i] + taxes[i] + tips[i]))
            .ToList());
    }

    private static Result<IReadOnlyList<SplitPart>> InvalidSplit(string message) =>
        Result.Fail<IReadOnlyList<SplitPart>>(new Error(ErrorCode.InvalidSplit, message));

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
}
