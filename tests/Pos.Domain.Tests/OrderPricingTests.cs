using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>
/// Order pricing: subtotal → discount → tax → tip, each rounded to cents (AwayFromZero) before the next step.
/// Bases: tax on (subtotal − discount); tip on (subtotal − discount), i.e. pre-tax.
/// Midpoint cases use amounts whose cent digit is even, so ToEven would round the other way.
/// </summary>
public class OrderPricingTests
{
    private static readonly Percentage NycTax = new(8.875m);

    private static Order OrderWith(decimal subtotal, Percentage taxRate)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, taxRate);
        order.AddItem(new MenuItem("Item", Money.Usd(subtotal)), 1, notes: null);
        return order;
    }

    [Fact]
    public void Percent_discount_is_rounded_away_from_zero_at_the_midpoint()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m))); // 1.725

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(1.73m), totals.Discount);
        Assert.Equal(Money.Usd(9.77m), totals.Total);
    }

    [Fact]
    public void Fixed_discount_is_subtracted_exactly()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(2.00m)));

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(2.00m), totals.Discount);
        Assert.Equal(Money.Usd(9.50m), totals.Total);
    }

    [Fact]
    public void Discount_equal_to_subtotal_leaves_nothing_to_tax_or_tip()
    {
        var order = OrderWith(11.50m, NycTax);
        order.SetTip(new Percentage(18m));

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(11.50m)));

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.00m), totals.Tax);
        Assert.Equal(Money.Usd(0.00m), totals.Tip);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public void Fixed_discount_above_subtotal_is_a_domain_error()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        Assert.Throws<DomainException>(() => order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(11.51m))));
        Assert.Null(order.Discount);
    }

    [Fact]
    public void Percent_discount_above_100_is_a_domain_error()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        Assert.Throws<DomainException>(
            () => order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(100.01m))));
    }

    [Fact]
    public void Applying_a_discount_replaces_the_previous_one_and_it_can_be_removed()
    {
        var order = OrderWith(10.00m, Percentage.Zero);
        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(2.00m)));

        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(10m)));
        Assert.Equal(Money.Usd(1.00m), order.CalculateTotals().Discount);

        order.RemoveDiscount();
        Assert.Null(order.Discount);
        Assert.Equal(Money.Usd(10.00m), order.CalculateTotals().Total);
    }

    [Fact]
    public void Tax_is_rounded_to_cents()
    {
        var totals = OrderWith(10.00m, NycTax).CalculateTotals(); // 0.8875

        Assert.Equal(Money.Usd(0.89m), totals.Tax);
        Assert.Equal(Money.Usd(10.89m), totals.Total);
    }

    [Fact]
    public void Tax_is_rounded_away_from_zero_at_the_midpoint()
    {
        var totals = OrderWith(12.00m, NycTax).CalculateTotals(); // 1.065

        Assert.Equal(Money.Usd(1.07m), totals.Tax);
        Assert.Equal(Money.Usd(13.07m), totals.Total);
    }

    [Fact]
    public void Tax_base_is_the_subtotal_after_discount()
    {
        var order = OrderWith(12.00m, NycTax);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(2.00m))); // tax base 10.00 → 0.8875

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.89m), totals.Tax);
        Assert.Equal(Money.Usd(10.89m), totals.Total);
    }

    [Fact]
    public void Tip_is_rounded_away_from_zero_at_the_midpoint()
    {
        var order = OrderWith(12.30m, Percentage.Zero);

        order.SetTip(new Percentage(15m)); // 1.845

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(1.85m), totals.Tip);
        Assert.Equal(Money.Usd(14.15m), totals.Total);
    }

    [Fact]
    public void Tip_base_is_pre_tax_so_the_tax_rate_does_not_change_the_tip()
    {
        var untaxed = OrderWith(12.00m, Percentage.Zero);
        var taxed = OrderWith(12.00m, NycTax);
        untaxed.SetTip(new Percentage(18m));
        taxed.SetTip(new Percentage(18m));

        Assert.Equal(Money.Usd(2.16m), untaxed.CalculateTotals().Tip);
        Assert.Equal(Money.Usd(2.16m), taxed.CalculateTotals().Tip);
        Assert.Equal(Money.Usd(15.23m), taxed.CalculateTotals().Total); // 12.00 + 1.07 + 2.16
    }

    [Fact]
    public void Tip_base_is_the_subtotal_after_discount()
    {
        var order = OrderWith(12.00m, Percentage.Zero);
        order.SetTip(new Percentage(18m));

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(2.00m))); // tip base 10.00

        Assert.Equal(Money.Usd(1.80m), order.CalculateTotals().Tip);
    }

    [Fact]
    public void Removing_the_tip_sets_it_to_zero()
    {
        var order = OrderWith(12.00m, Percentage.Zero);
        order.SetTip(new Percentage(18m));

        order.RemoveTip();

        Assert.Null(order.TipRate);
        Assert.Equal(Money.Usd(0.00m), order.CalculateTotals().Tip);
    }

    [Fact]
    public void Each_step_is_rounded_before_the_next_one_uses_it()
    {
        var order = OrderWith(5.00m, NycTax);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m)));
        order.SetTip(new Percentage(18m));

        var totals = order.CalculateTotals();

        // discount 0.75 → base 4.25; tax 0.3771875 → 0.38; tip 0.765 → 0.77.
        // Rounding only the final total would give 5.39 (4.25 + 0.3771875 + 0.765 = 5.3921875).
        Assert.Equal(Money.Usd(5.00m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.75m), totals.Discount);
        Assert.Equal(Money.Usd(0.38m), totals.Tax);
        Assert.Equal(Money.Usd(0.77m), totals.Tip);
        Assert.Equal(Money.Usd(5.40m), totals.Total);
    }

    [Fact]
    public void Tax_rate_is_snapshotted_at_creation()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, NycTax);

        Assert.Equal(NycTax, order.TaxRate);
    }
}
