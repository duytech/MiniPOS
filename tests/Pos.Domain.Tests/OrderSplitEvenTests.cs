using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>
/// Even split: the order is priced first, then its subtotal, discount, surcharge, tax and tip are each divided by the
/// number of parts, rounded down to the cent, and the leftover cents of each amount go to the first part.
/// Every amount of the parts must add up exactly to the order's amount.
/// </summary>
public class OrderSplitEvenTests
{
    private static readonly Percentage NycTax = new(8.875m);
    private static readonly Percentage CardSurcharge = new(3m);

    private static Order OrderWithLines(Percentage taxRate, params decimal[] prices)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, taxRate, CardSurcharge);
        for (var i = 0; i < prices.Length; i++)
        {
            order.AddItem(new MenuItem($"Item {i}", Money.Usd(prices[i])), 1, notes: null);
        }

        return order;
    }

    private static Money[] Usd(params decimal[] amounts) => amounts.Select(Money.Usd).ToArray();

    [Fact]
    public void Leftover_cent_goes_to_the_first_part()
    {
        var order = OrderWithLines(Percentage.Zero, 100.00m);

        var parts = order.SplitEvenly(3);

        // 100.00 / 3 = 33.333… → 33.33 each, leftover 0.01 to the first part.
        Assert.Equal(Usd(33.34m, 33.33m, 33.33m), parts.Select(p => p.Subtotal));
        Assert.Equal(Usd(33.34m, 33.33m, 33.33m), parts.Select(p => p.Total));
        Assert.All(parts, p => Assert.Empty(p.LineIds));
    }

    [Fact]
    public void More_parts_than_cents_leaves_zero_parts()
    {
        var order = OrderWithLines(Percentage.Zero, 0.05m);

        var parts = order.SplitEvenly(10);

        Assert.Equal([Money.Usd(0.05m), .. Enumerable.Repeat(Money.Usd(0.00m), 9)], parts.Select(p => p.Total));
    }

    [Fact]
    public void One_part_is_the_whole_order()
    {
        var order = OrderWithLines(NycTax, 3.33m, 3.33m, 3.33m);
        order.SetTip(new Percentage(18m));
        var totals = order.CalculateTotals();

        var part = Assert.Single(order.SplitEvenly(1));

        Assert.Equal(
            (totals.Subtotal, totals.Discount, totals.Surcharge, totals.Tax, totals.Tip, totals.Total),
            (part.Subtotal, part.Discount, part.Surcharge, part.Tax, part.Tip, part.Total));
    }

    [Fact]
    public void Discount_surcharge_tax_and_tip_are_each_divided_separately()
    {
        var order = OrderWithLines(NycTax, 3.33m, 3.33m, 3.33m);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m)));
        order.SetPaymentMethod(PaymentMethod.Card);
        order.SetTip(new Percentage(18m));
        // Order: subtotal 9.99, discount 1.50, surcharge 0.25, tax 0.78, tip 1.53, total 11.05.

        var parts = order.SplitEvenly(4);

        Assert.Equal(Usd(2.52m, 2.49m, 2.49m, 2.49m), parts.Select(p => p.Subtotal)); // 9.99 / 4 = 2.4975
        Assert.Equal(Usd(0.39m, 0.37m, 0.37m, 0.37m), parts.Select(p => p.Discount)); // 1.50 / 4 = 0.375
        Assert.Equal(Usd(0.07m, 0.06m, 0.06m, 0.06m), parts.Select(p => p.Surcharge)); // 0.25 / 4 = 0.0625
        Assert.Equal(Usd(0.21m, 0.19m, 0.19m, 0.19m), parts.Select(p => p.Tax)); // 0.78 / 4 = 0.195: rounds down, not up
        Assert.Equal(Usd(0.39m, 0.38m, 0.38m, 0.38m), parts.Select(p => p.Tip)); // 1.53 / 4 = 0.3825
        Assert.Equal(Usd(2.80m, 2.75m, 2.75m, 2.75m), parts.Select(p => p.Total));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(11)]
    public void Every_amount_of_the_parts_adds_up_to_the_order(int partCount)
    {
        var order = OrderWithLines(NycTax, 12.99m, 7.45m, 0.99m, 23.10m);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(12.5m)));
        order.SetPaymentMethod(PaymentMethod.Card);
        order.SetTip(new Percentage(20m));
        var totals = order.CalculateTotals();

        var parts = order.SplitEvenly(partCount);

        Assert.Equal(partCount, parts.Count);
        Assert.Equal(totals.Subtotal, Sum(parts.Select(p => p.Subtotal)));
        Assert.Equal(totals.Discount, Sum(parts.Select(p => p.Discount)));
        Assert.Equal(totals.Surcharge, Sum(parts.Select(p => p.Surcharge)));
        Assert.Equal(totals.Tax, Sum(parts.Select(p => p.Tax)));
        Assert.Equal(totals.Tip, Sum(parts.Select(p => p.Tip)));
        Assert.Equal(totals.Total, Sum(parts.Select(p => p.Total)));
        Assert.All(parts.Skip(1), p => Assert.Equal(parts[^1].Total, p.Total));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Needs_at_least_one_part(int partCount)
    {
        var order = OrderWithLines(NycTax, 10.00m);

        Assert.Throws<DomainException>(() => order.SplitEvenly(partCount));
    }

    private static Money Sum(IEnumerable<Money> amounts) =>
        amounts.Aggregate(Money.Zero(Currency.USD), (sum, amount) => sum + amount);
}
