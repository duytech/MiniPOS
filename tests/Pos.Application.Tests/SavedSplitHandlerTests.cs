using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

/// <summary>
/// The saved split by item (SaveSplit, GetSplit, PreviewSplit, RemoveSplit handlers): only the item → part assignment
/// is stored; its amounts are recomputed on every read. Save, preview and remove need an open order; reading works in
/// any status. Only a real change of the order clears it; a no-op, a rejected change, pay and cancel keep it.
/// </summary>
public class SavedSplitHandlerTests
{
    private static readonly Percentage NycTax = new(8.875m);
    private static readonly Percentage CardSurcharge = new(3m);

    private readonly OrderActions _act = new();

    private async Task<(Order Order, Guid[] ItemIds)> OrderWithItems(params decimal[] prices)
    {
        var order = _act.NewOrder(NycTax, CardSurcharge);
        var itemIds = new List<Guid>();
        foreach (var price in prices)
        {
            itemIds.Add(await _act.AddPricedItem(order, price));
        }

        return (order, itemIds.ToArray());
    }

    private static Guid[] Part(params Guid[] itemIds) => itemIds;

    /// <summary>An order with two items and a saved split of one item per part.</summary>
    private async Task<(Order Order, Guid[] ItemIds)> OrderWithSavedSplit()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));
        return (order, l);
    }

    /// <summary>An order with a 10% discount, a 15% tip and cash, and a saved split.</summary>
    private async Task<(Order Order, Guid[] ItemIds)> OrderWithAdjustmentsAndSavedSplit()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);
        await _act.ApplyPercentDiscount(order, 10m);
        await _act.SetTip(order, 15m);
        await _act.SetPaymentMethod(order, PaymentMethod.Cash);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));
        return (order, l);
    }

    private async Task<IEnumerable<IReadOnlyList<Guid>>> SavedItemIds(Order order) =>
        (await _act.GetSplit(order)).Data!.Parts.Select(p => p.ItemIds);

    [Fact]
    public async Task Saved_split_reads_back_with_exact_cents()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);

        var saved = (await _act.SaveSplit(order, Part(l[0]), Part(l[1]))).Data!;
        var read = (await _act.GetSplit(order)).Data!;

        // Order tax 8.875% of 100.00 = 8.875 → 8.88 (midpoint, away from zero).
        // Shares: 8.88 × 60/100 = 5.328 → 5.32, 8.88 × 40/100 = 3.552 → 3.55; leftover 0.01 to the 60.00 part.
        Assert.Equal(Money.Usd(8.88m), OrderPricing.CalculateTotals(order).Tax);
        foreach (var split in new[] { saved, read })
        {
            Assert.Equal([Money.Usd(60.00m), Money.Usd(40.00m)], split.Parts.Select(p => p.Subtotal));
            Assert.Equal([Money.Usd(5.33m), Money.Usd(3.55m)], split.Parts.Select(p => p.Tax));
            Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], split.Parts.Select(p => p.Total));
            Assert.Equal([[l[0]], [l[1]]], split.Parts.Select(p => p.ItemIds));
        }
    }

    [Fact]
    public async Task Saved_split_shares_discount_surcharge_tax_and_tip()
    {
        var (order, l) = await OrderWithItems(6.00m, 4.00m);
        await _act.ApplyPercentDiscount(order, 15m);
        await _act.SetPaymentMethod(order, PaymentMethod.Card);
        await _act.SetTip(order, 18m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));

        var totals = OrderPricing.CalculateTotals(order);
        var parts = (await _act.GetSplit(order)).Data!.Parts;

        // Order: subtotal 10.00, discount 1.50, surcharge 3% of 8.50 = 0.255 → 0.26, tax 8.875% of 8.76 = 0.77745 → 0.78,
        // tip 18% of 8.50 = 1.53, total 11.07.
        Assert.Equal(
            [Money.Usd(10.00m), Money.Usd(1.50m), Money.Usd(0.26m), Money.Usd(0.78m), Money.Usd(1.53m), Money.Usd(11.07m)],
            [totals.Subtotal, totals.Discount, totals.Surcharge, totals.Tax, totals.Tip, totals.Total]);
        Assert.Equal([Money.Usd(0.90m), Money.Usd(0.60m)], parts.Select(p => p.Discount));
        Assert.Equal([Money.Usd(0.16m), Money.Usd(0.10m)], parts.Select(p => p.Surcharge)); // 0.156 → 0.15 + 0.01, 0.104 → 0.10
        Assert.Equal([Money.Usd(0.47m), Money.Usd(0.31m)], parts.Select(p => p.Tax)); // 0.468 → 0.46 + 0.01, 0.312 → 0.31
        Assert.Equal([Money.Usd(0.92m), Money.Usd(0.61m)], parts.Select(p => p.Tip)); // 0.918 → 0.91 + 0.01, 0.612 → 0.61
        Assert.Equal([Money.Usd(6.65m), Money.Usd(4.42m)], parts.Select(p => p.Total));
    }

    [Fact]
    public async Task Preview_prices_the_split_without_saving_it()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);

        var parts = (await _act.PreviewSplit(order, Part(l[0]), Part(l[1]))).Data!.Parts;

        Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], parts.Select(p => p.Total));
        Assert.Empty(order.SavedSplitParts);
    }

    [Fact]
    public async Task No_saved_split_is_not_found()
    {
        var (order, _) = await OrderWithItems(60.00m, 40.00m);

        Assert.Equal(ErrorCode.SplitNotFound, (await _act.GetSplit(order)).Error!.Code);
        Assert.Equal(ErrorCode.SplitNotFound, (await _act.RemoveSplit(order)).Error!.Code);
    }

    [Fact]
    public async Task A_second_save_replaces_the_first()
    {
        var (order, l) = await OrderWithItems(1.00m, 2.00m, 3.00m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1], l[2]));

        await _act.SaveSplit(order, Part(l[2]), Part(l[0]), Part(l[1]));

        var parts = (await _act.GetSplit(order)).Data!.Parts;
        Assert.Equal([[l[2]], [l[0]], [l[1]]], parts.Select(p => p.ItemIds));
        Assert.Equal([Money.Usd(3.00m), Money.Usd(1.00m), Money.Usd(2.00m)], parts.Select(p => p.Subtotal));
        Assert.Equal([0, 1, 2], order.SavedSplitParts.Select(p => p.Position).Order());
    }

    [Fact]
    public async Task An_invalid_split_is_not_saved_and_keeps_the_saved_one()
    {
        var (order, l) = await OrderWithSavedSplit();

        Assert.Equal(ErrorCode.InvalidSplit, (await _act.SaveSplit(order, Part(l[0], l[1]))).Error!.Code);

        Assert.Equal([[l[0]], [l[1]]], await SavedItemIds(order));
    }

    [Fact]
    public async Task Remove_clears_the_saved_split()
    {
        var (order, _) = await OrderWithSavedSplit();

        Assert.True((await _act.RemoveSplit(order)).Data!);

        Assert.Empty(order.SavedSplitParts);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Saved_split_can_still_be_read_after_the_order_is_closed(OrderStatus status)
    {
        var (order, _) = await OrderWithSavedSplit();

        await _act.Close(order, status);

        Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], (await _act.GetSplit(order)).Data!.Parts.Select(p => p.Total));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Save_preview_and_remove_need_an_open_order(OrderStatus status)
    {
        var (order, l) = await OrderWithSavedSplit();
        await _act.Close(order, status);
        var expected = $"The order is {status}; only an open order can be changed.";

        Assert.Equal(expected, (await _act.SaveSplit(order, Part(l[1]), Part(l[0]))).Error!.Message);
        Assert.Equal(expected, (await _act.PreviewSplit(order, Part(l[1]), Part(l[0]))).Error!.Message);
        Assert.Equal(expected, (await _act.RemoveSplit(order)).Error!.Message);
        Assert.Equal([[l[0]], [l[1]]], await SavedItemIds(order));
    }

    public static TheoryData<string> RealChanges =>
    [
        "add-item", "remove-item", "change-quantity", "apply-other-discount", "remove-discount", "set-other-tip",
        "remove-tip", "set-other-payment-method",
    ];

    [Theory]
    [MemberData(nameof(RealChanges))]
    public async Task A_real_change_clears_the_saved_split(string change)
    {
        var (order, l) = await OrderWithAdjustmentsAndSavedSplit();

        var result = change switch
        {
            "add-item" => await _act.AddItem(order, OrderActions.Item("Tea", 2.00m), 1),
            "remove-item" => await _act.RemoveItem(order, l[1]),
            "change-quantity" => await _act.ChangeQuantity(order, l[0], 2),
            "apply-other-discount" => await _act.ApplyPercentDiscount(order, 20m),
            "remove-discount" => await _act.RemoveDiscount(order),
            "set-other-tip" => await _act.SetTip(order, 18m),
            "remove-tip" => await _act.RemoveTip(order),
            _ => await _act.SetPaymentMethod(order, PaymentMethod.Card),
        };

        Assert.True(result.IsSuccess);
        Assert.Empty(order.SavedSplitParts);
    }

    [Fact]
    public async Task Adding_more_of_an_existing_item_clears_the_saved_split()
    {
        var order = _act.NewOrder(NycTax, CardSurcharge);
        var pho = OrderActions.Item("Pho", 9.50m);
        await _act.AddItem(order, pho, 1);
        var tea = await _act.AddPricedItem(order, 2.00m);
        await _act.SaveSplit(order, Part(order.Items[0].Id), Part(tea));

        await _act.AddItem(order, pho, 1);

        Assert.Equal(2, order.Items.Count);
        Assert.Empty(order.SavedSplitParts);
    }

    public static TheoryData<string> NoOps =>
    [
        "same-quantity", "same-discount", "remove-missing-discount", "same-tip", "remove-missing-tip",
        "same-payment-method",
    ];

    /// <summary>
    /// "remove-missing-*" start from an order without that adjustment; the others from an order with a 10% discount,
    /// a 15% tip and cash.
    /// </summary>
    [Theory]
    [MemberData(nameof(NoOps))]
    public async Task A_no_op_keeps_the_saved_split(string change)
    {
        var (order, l) = change.StartsWith("remove-missing-", StringComparison.Ordinal)
            ? await OrderWithSavedSplit()
            : await OrderWithAdjustmentsAndSavedSplit();

        var result = change switch
        {
            "same-quantity" => await _act.ChangeQuantity(order, l[0], 1),
            "same-discount" => await _act.ApplyPercentDiscount(order, 10.0m),
            "remove-missing-discount" => await _act.RemoveDiscount(order),
            "same-tip" => await _act.SetTip(order, 15m),
            "remove-missing-tip" => await _act.RemoveTip(order),
            _ => await _act.SetPaymentMethod(order, PaymentMethod.Cash),
        };

        Assert.True(result.IsSuccess);
        Assert.Equal([[l[0]], [l[1]]], await SavedItemIds(order));
    }

    [Fact]
    public async Task Same_fixed_discount_keeps_and_a_different_one_clears_the_saved_split()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);
        await _act.ApplyFixedDiscount(order, 5.00m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));

        await _act.ApplyFixedDiscount(order, 5.00m);
        Assert.NotEmpty(order.SavedSplitParts);

        await _act.ApplyFixedDiscount(order, 5.01m);
        Assert.Empty(order.SavedSplitParts);
    }

    [Fact]
    public async Task Switching_a_percent_discount_to_the_same_fixed_value_clears_the_saved_split()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);
        await _act.ApplyPercentDiscount(order, 5m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));

        await _act.ApplyFixedDiscount(order, 5.00m);

        Assert.Empty(order.SavedSplitParts);
    }

    [Fact]
    public async Task A_rejected_change_keeps_the_saved_split()
    {
        var (order, l) = await OrderWithItems(60.00m, 40.00m);
        await _act.ApplyFixedDiscount(order, 50.00m);
        await _act.SaveSplit(order, Part(l[0]), Part(l[1]));

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, (await _act.RemoveItem(order, l[0])).Error!.Code);

        Assert.NotEmpty(order.SavedSplitParts);
    }
}
