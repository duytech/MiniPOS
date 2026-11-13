using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>Prices a split of the bill from the order's totals (<see cref="OrderPricing.CalculateTotals"/>).</summary>
public static class OrderSplitting
{
    /// <summary>
    /// Splits the bill by item: each part is a set of whole items and every item is in exactly one part.
    /// The order's discount, surcharge, tax and tip are each shared by the part's share of the subtotal, rounded down
    /// to the cent; the leftover cents go to the part with the largest subtotal (the earlier part on a tie).
    /// So every amount of the parts adds up exactly to the order's amount, and the part totals to the order total.
    /// A split needs at least two parts. Nothing is stored and any status is allowed.
    /// </summary>
    public static Result<IReadOnlyList<SplitPart>> Split(Order order, IReadOnlyList<IReadOnlyCollection<Guid>> itemIdsPerPart)
    {
        if (itemIdsPerPart.Count < 2)
        {
            return InvalidSplit("A split needs at least two parts.");
        }

        var linesById = order.Items.ToDictionary(l => l.Id);
        var assignedItemIds = new HashSet<Guid>();
        var partSubtotals = new List<Money>();
        foreach (var itemIds in itemIdsPerPart)
        {
            if (itemIds.Count == 0)
            {
                return InvalidSplit("Every part of a split needs at least one item.");
            }

            var partSubtotal = Money.Zero(order.Currency);
            foreach (var itemId in itemIds)
            {
                if (!linesById.TryGetValue(itemId, out var item))
                {
                    return Result.Fail<IReadOnlyList<SplitPart>>(OrderErrors.ItemNotOnOrder(itemId));
                }

                if (!assignedItemIds.Add(itemId))
                {
                    return InvalidSplit($"Item {itemId} is in more than one part.");
                }

                partSubtotal += OrderPricing.ItemTotal(item);
            }

            partSubtotals.Add(partSubtotal);
        }

        if (assignedItemIds.Count != order.Items.Count)
        {
            return InvalidSplit("Every item of the order must be in a part of the split.");
        }

        var totals = OrderPricing.CalculateTotals(order);
        var discounts = AllocateAmountToPart(totals.Discount, partSubtotals, totals.Subtotal);
        var surcharges = AllocateAmountToPart(totals.Surcharge, partSubtotals, totals.Subtotal);
        var taxes = AllocateAmountToPart(totals.Tax, partSubtotals, totals.Subtotal);
        var tips = AllocateAmountToPart(totals.Tip, partSubtotals, totals.Subtotal);

        return Result.Ok<IReadOnlyList<SplitPart>>(itemIdsPerPart.Select((itemIds, i) => new SplitPart(
                itemIds.ToList(),
                partSubtotals[i],
                discounts[i],
                surcharges[i],
                taxes[i],
                tips[i],
                Total: partSubtotals[i] - discounts[i] + surcharges[i] + taxes[i] + tips[i]))
            .ToList());
    }

    /// <summary>The item ids of the order's saved split, per part, in saved order.</summary>
    public static IReadOnlyList<IReadOnlyCollection<Guid>> SavedSplitItemIds(Order order) => order.SavedSplitParts
        .OrderBy(p => p.Position)
        .Select(p => (IReadOnlyCollection<Guid>)p.Items.OrderBy(l => l.Position).Select(l => l.OrderMenuItemId).ToList())
        .ToList();

    /// <summary>
    /// Splits the bill evenly between <paramref name="parts"/> people. The order is priced first; then its subtotal,
    /// discount, surcharge, tax and tip are each divided by the number of parts, rounded down to the cent, and the
    /// leftover cents of each amount go to the first part. So every amount of the parts adds up exactly to the order's
    /// amount, and the part totals to the order total. Parts hold no items.
    /// </summary>
    public static Result<IReadOnlyList<SplitPart>> SplitEvenly(Order order, int parts)
    {
        if (parts < 1)
        {
            return InvalidSplit("An even split needs at least one part.");
        }

        var totals = OrderPricing.CalculateTotals(order);
        var subtotals = AllocateMoneyEqually(totals.Subtotal, parts);
        var discounts = AllocateMoneyEqually(totals.Discount, parts);
        var surcharges = AllocateMoneyEqually(totals.Surcharge, parts);
        var taxes = AllocateMoneyEqually(totals.Tax, parts);
        var tips = AllocateMoneyEqually(totals.Tip, parts);

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
    /// Shares <paramref name="amountToShare"/> by each part's share of the subtotal, rounded down to the cent; the
    /// leftover cents go to the part with the largest subtotal.
    /// </summary>
    /// <param name="amountToShare">The order-level amount to share, e.g. the whole order's tax.</param>
    /// <param name="partSubtotals">One entry per split part: the sum of the item totals of the items in that part.</param>
    /// <param name="subtotal">The whole order's subtotal (sum of all item totals), the denominator of each part's
    /// share; equals the sum of <paramref name="partSubtotals"/> because every item is in exactly one part.</param>
    /// <returns>One share per part, in the order of <paramref name="partSubtotals"/>; the shares add up to
    /// <paramref name="amountToShare"/>.</returns>
    private static Money[] AllocateAmountToPart(Money amountToShare, List<Money> partSubtotals, Money subtotal)
    {
        var shares = partSubtotals
            .Select(partSubtotal => subtotal.Amount == 0m
                ? Money.Zero(amountToShare.Currency)
                : Money.RoundDownToCent(
                    amountToShare.Amount * partSubtotal.Amount / subtotal.Amount, amountToShare.Currency))
            .ToArray();

        var leftover = shares.Aggregate(amountToShare, (rest, share) => rest - share);
        shares[FindIndexOfLargest(partSubtotals)] += leftover;
        return shares;
    }

    /// <summary>Returns the index of the largest amount; on a tie, the first one.</summary>
    private static int FindIndexOfLargest(List<Money> amounts)
    {
        var largest = 0;
        for (var i = 1; i < amounts.Count; i++)
        {
            if (amounts[i].Amount > amounts[largest].Amount)
            {
                largest = i;
            }
        }

        return largest;
    }

    /// <summary>
    /// Divides <paramref name="amount"/> into <paramref name="parts"/> equal shares rounded down to the cent; the
    /// leftover cents go to the first share.
    /// </summary>
    private static Money[] AllocateMoneyEqually(Money amount, int parts)
    {
        var share = Money.RoundDownToCent(amount.Amount / parts, amount.Currency);
        var shares = Enumerable.Repeat(share, parts).ToArray();
        var otherPartsTotal = share * (parts - 1);
        var firstShare = amount - otherPartsTotal;
        shares[0] = firstShare;
        return shares;
    }
}
