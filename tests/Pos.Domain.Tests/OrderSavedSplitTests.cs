using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>
/// The saved split by item: only the line → part assignment is stored; its amounts are recomputed by
/// <see cref="Order.Split"/> on every read. Save, preview and remove need an open order; reading works in any status.
/// Only a real change of the order clears it; a no-op, pay and cancel keep it.
/// </summary>
public class OrderSavedSplitTests
{
    private static readonly Percentage NycTax = new(8.875m);
    private static readonly Percentage CardSurcharge = new(3m);

    private static (Order Order, Guid[] LineIds) OrderWithLines(params decimal[] prices)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, NycTax, CardSurcharge);
        var lineIds = prices
            .Select((price, i) => order.AddItem(new MenuItem($"Item {i}", Money.Usd(price)), 1, notes: null).Id)
            .ToArray();
        return (order, lineIds);
    }

    private static Guid[] Part(params Guid[] lineIds) => lineIds;

    /// <summary>An order with two lines and a saved split of one line per part.</summary>
    private static (Order Order, Guid[] LineIds) OrderWithSavedSplit()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);
        order.SaveSplit([Part(l[0]), Part(l[1])]);
        return (order, l);
    }

    [Fact]
    public void Saved_split_reads_back_with_exact_cents()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);

        var saved = order.SaveSplit([Part(l[0]), Part(l[1])]);
        var read = order.GetSavedSplit();

        // Order tax 8.875% of 100.00 = 8.875 → 8.88 (midpoint, away from zero).
        // Shares: 8.88 × 60/100 = 5.328 → 5.32, 8.88 × 40/100 = 3.552 → 3.55; leftover 0.01 to the 60.00 part.
        Assert.Equal(Money.Usd(8.88m), order.CalculateTotals().Tax);
        Assert.NotNull(read);
        foreach (var parts in new[] { saved, read })
        {
            Assert.Equal([Money.Usd(60.00m), Money.Usd(40.00m)], parts.Select(p => p.Subtotal));
            Assert.Equal([Money.Usd(5.33m), Money.Usd(3.55m)], parts.Select(p => p.Tax));
            Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], parts.Select(p => p.Total));
            Assert.Equal([[l[0]], [l[1]]], parts.Select(p => p.LineIds));
        }
    }

    [Fact]
    public void Saved_split_shares_discount_surcharge_tax_and_tip()
    {
        var (order, l) = OrderWithLines(6.00m, 4.00m);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m)));
        order.SetPaymentMethod(PaymentMethod.Card);
        order.SetTip(new Percentage(18m));
        order.SaveSplit([Part(l[0]), Part(l[1])]);

        var totals = order.CalculateTotals();
        var parts = order.GetSavedSplit()!;

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
    public void Preview_prices_the_split_without_saving_it()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);

        var parts = order.PreviewSplit([Part(l[0]), Part(l[1])]);

        Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], parts.Select(p => p.Total));
        Assert.False(order.HasSavedSplit);
        Assert.Null(order.GetSavedSplit());
    }

    [Fact]
    public void No_saved_split_reads_as_null()
    {
        var (order, _) = OrderWithLines(60.00m, 40.00m);

        Assert.False(order.HasSavedSplit);
        Assert.Null(order.GetSavedSplit());
    }

    [Fact]
    public void A_second_save_replaces_the_first()
    {
        var (order, l) = OrderWithLines(1.00m, 2.00m, 3.00m);
        order.SaveSplit([Part(l[0]), Part(l[1], l[2])]);

        order.SaveSplit([Part(l[2]), Part(l[0]), Part(l[1])]);

        var parts = order.GetSavedSplit()!;
        Assert.Equal([[l[2]], [l[0]], [l[1]]], parts.Select(p => p.LineIds));
        Assert.Equal([Money.Usd(3.00m), Money.Usd(1.00m), Money.Usd(2.00m)], parts.Select(p => p.Subtotal));
        Assert.Equal([0, 1, 2], order.SavedSplitParts.Select(p => p.Position).Order());
    }

    [Fact]
    public void An_invalid_split_is_not_saved_and_keeps_the_saved_one()
    {
        var (order, l) = OrderWithSavedSplit();

        Assert.Throws<DomainException>(() => order.SaveSplit([Part(l[0], l[1])]));

        Assert.Equal([[l[0]], [l[1]]], order.GetSavedSplit()!.Select(p => p.LineIds));
    }

    [Fact]
    public void Remove_clears_the_saved_split()
    {
        var (order, _) = OrderWithSavedSplit();

        order.RemoveSplit();

        Assert.False(order.HasSavedSplit);
        Assert.Null(order.GetSavedSplit());
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public void Saved_split_can_still_be_read_after_the_order_is_closed(string status)
    {
        var (order, _) = OrderWithSavedSplit();

        Close(order, status);

        Assert.Equal([Money.Usd(65.33m), Money.Usd(43.55m)], order.GetSavedSplit()!.Select(p => p.Total));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public void Save_preview_and_remove_need_an_open_order(string status)
    {
        var (order, l) = OrderWithSavedSplit();
        Close(order, status);
        var expected = $"The order is {status}; only an open order can be changed.";

        Assert.Equal(expected, Assert.Throws<DomainException>(() => order.SaveSplit([Part(l[1]), Part(l[0])])).Message);
        Assert.Equal(expected, Assert.Throws<DomainException>(() => order.PreviewSplit([Part(l[1]), Part(l[0])])).Message);
        Assert.Equal(expected, Assert.Throws<DomainException>(order.RemoveSplit).Message);
        Assert.Equal([[l[0]], [l[1]]], order.GetSavedSplit()!.Select(p => p.LineIds));
    }

    public static TheoryData<string> RealChanges =>
    [
        "add-item", "remove-line", "change-quantity", "apply-other-discount", "remove-discount", "set-other-tip",
        "remove-tip", "set-other-payment-method",
    ];

    /// <summary>Each starts from an order with a 10% discount, a 15% tip and cash, and a saved split.</summary>
    [Theory]
    [MemberData(nameof(RealChanges))]
    public void A_real_change_clears_the_saved_split(string change)
    {
        var (order, l) = OrderWithAdjustmentsAndSavedSplit();

        switch (change)
        {
            case "add-item": order.AddItem(new MenuItem("Tea", Money.Usd(2.00m)), 1, notes: null); break;
            case "remove-line": order.RemoveLine(order.Lines.Single(line => line.Id == l[1])); break;
            case "change-quantity": order.ChangeLineQuantity(order.Lines.Single(line => line.Id == l[0]), 2); break;
            case "apply-other-discount": order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(20m))); break;
            case "remove-discount": order.RemoveDiscount(); break;
            case "set-other-tip": order.SetTip(new Percentage(18m)); break;
            case "remove-tip": order.RemoveTip(); break;
            default: order.SetPaymentMethod(PaymentMethod.Card); break;
        }

        Assert.False(order.HasSavedSplit);
        Assert.Null(order.GetSavedSplit());
    }

    [Fact]
    public void Adding_more_of_an_existing_line_clears_the_saved_split()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, NycTax, CardSurcharge);
        var pho = new MenuItem("Pho", Money.Usd(9.50m));
        var l0 = order.AddItem(pho, 1, notes: null).Id;
        var l1 = order.AddItem(new MenuItem("Tea", Money.Usd(2.00m)), 1, notes: null).Id;
        order.SaveSplit([Part(l0), Part(l1)]);

        order.AddItem(pho, 1, notes: null);

        Assert.Equal(2, order.Lines.Count);
        Assert.False(order.HasSavedSplit);
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
    public void A_no_op_keeps_the_saved_split(string change)
    {
        var (order, l) = change.StartsWith("remove-missing-", StringComparison.Ordinal)
            ? OrderWithSavedSplit()
            : OrderWithAdjustmentsAndSavedSplit();

        switch (change)
        {
            case "same-quantity": order.ChangeLineQuantity(order.Lines.Single(line => line.Id == l[0]), 1); break;
            case "same-discount": order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(10.0m))); break;
            case "remove-missing-discount": order.RemoveDiscount(); break;
            case "same-tip": order.SetTip(new Percentage(15m)); break;
            case "remove-missing-tip": order.RemoveTip(); break;
            default: order.SetPaymentMethod(PaymentMethod.Cash); break;
        }

        Assert.Equal([[l[0]], [l[1]]], order.GetSavedSplit()!.Select(p => p.LineIds));
    }

    [Fact]
    public void Same_fixed_discount_keeps_and_a_different_one_clears_the_saved_split()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);
        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(5.00m)));
        order.SaveSplit([Part(l[0]), Part(l[1])]);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(5.00m)));
        Assert.True(order.HasSavedSplit);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(5.01m)));
        Assert.False(order.HasSavedSplit);
    }

    [Fact]
    public void Switching_a_percent_discount_to_the_same_fixed_value_clears_the_saved_split()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(5m)));
        order.SaveSplit([Part(l[0]), Part(l[1])]);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(5.00m)));

        Assert.False(order.HasSavedSplit);
    }

    [Fact]
    public void A_rejected_change_keeps_the_saved_split()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);
        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(50.00m)));
        order.SaveSplit([Part(l[0]), Part(l[1])]);

        Assert.Throws<DomainException>(() => order.RemoveLine(order.Lines.Single(line => line.Id == l[0])));

        Assert.True(order.HasSavedSplit);
    }

    private static (Order Order, Guid[] LineIds) OrderWithAdjustmentsAndSavedSplit()
    {
        var (order, l) = OrderWithLines(60.00m, 40.00m);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(10m)));
        order.SetTip(new Percentage(15m));
        order.SetPaymentMethod(PaymentMethod.Cash);
        order.SaveSplit([Part(l[0]), Part(l[1])]);
        return (order, l);
    }

    private static void Close(Order order, string status)
    {
        if (status == "Paid")
        {
            order.Pay(DateTimeOffset.UtcNow);
        }
        else
        {
            order.Cancel(DateTimeOffset.UtcNow);
        }
    }
}
