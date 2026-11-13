using Pos.Domain.Common;
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
        var order = TestOrders.New(taxRate);
        TestOrders.AddPricedItem(order, subtotal);
        return order;
    }

    [Fact]
    public void New_order_has_zero_totals()
    {
        var totals = OrderPricing.CalculateTotals(TestOrders.New());

        Assert.Equal(Money.Usd(0.00m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public void Item_total_is_unit_price_times_quantity()
    {
        var item = TestOrders.AddPricedItem(TestOrders.New(), 3.35m, 3);

        Assert.Equal(Money.Usd(10.05m), OrderPricing.CalculateItemTotal(item));
    }

    [Fact]
    public void Subtotal_is_the_sum_of_item_totals_and_total_equals_subtotal()
    {
        var order = TestOrders.New();
        TestOrders.AddPricedItem(order, 12.99m, 2); // 25.98
        TestOrders.AddPricedItem(order, 3.35m, 3); // 10.05

        var totals = OrderPricing.CalculateTotals(order);

        Assert.Equal(Money.Usd(36.03m), totals.Subtotal);
        Assert.Equal(Money.Usd(36.03m), totals.Total);
    }

    [Fact]
    public void Sums_without_floating_point_drift()
    {
        var order = TestOrders.New();
        TestOrders.AddPricedItem(order, 0.10m);
        TestOrders.AddPricedItem(order, 0.20m);

        Assert.Equal(Money.Usd(0.30m), OrderPricing.CalculateTotals(order).Total);
    }

    [Fact]
    public void Subtotal_with_quantity_replaces_only_that_item_and_leaves_the_order_unchanged()
    {
        var order = TestOrders.New();
        var burger = TestOrders.AddPricedItem(order, 12.99m, 2); // 25.98
        TestOrders.AddPricedItem(order, 10.03m);

        Assert.Equal(Money.Usd(10.03m), OrderPricing.CalculateSubtotalWithQuantity(order, burger, 0));
        Assert.Equal(Money.Usd(23.02m), OrderPricing.CalculateSubtotalWithQuantity(order, burger, 1));
        Assert.Equal(Money.Usd(74.98m), OrderPricing.CalculateSubtotalWithQuantity(order, burger, 5)); // 64.95 + 10.03
        Assert.Equal(2, burger.Quantity);
        Assert.Equal(Money.Usd(36.01m), OrderPricing.CalculateSubtotal(order));
    }

    [Fact]
    public void Percent_discount_is_rounded_away_from_zero_at_the_midpoint()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        order.DiscountRate = new Percentage(15m); // 1.725

        var totals = OrderPricing.CalculateTotals(order);
        Assert.Equal(Money.Usd(1.73m), totals.Discount);
        Assert.Equal(Money.Usd(9.77m), totals.Total);
    }

    [Fact]
    public void Fixed_discount_is_subtracted_exactly()
    {
        var order = OrderWith(11.50m, Percentage.Zero);

        order.DiscountAmount = Money.Usd(2.00m);

        var totals = OrderPricing.CalculateTotals(order);
        Assert.Equal(Money.Usd(2.00m), totals.Discount);
        Assert.Equal(Money.Usd(9.50m), totals.Total);
    }

    [Fact]
    public void Discount_equal_to_subtotal_leaves_nothing_to_tax_or_tip()
    {
        var order = OrderWith(11.50m, NycTax);
        order.TipRate = new Percentage(18m);

        order.DiscountAmount = Money.Usd(11.50m);

        var totals = OrderPricing.CalculateTotals(order);
        Assert.Equal(Money.Usd(0.00m), totals.Tax);
        Assert.Equal(Money.Usd(0.00m), totals.Tip);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public void Tax_is_rounded_to_cents()
    {
        var totals = OrderPricing.CalculateTotals(OrderWith(10.00m, NycTax)); // 0.8875

        Assert.Equal(Money.Usd(0.89m), totals.Tax);
        Assert.Equal(Money.Usd(10.89m), totals.Total);
    }

    [Fact]
    public void Tax_is_rounded_away_from_zero_at_the_midpoint()
    {
        var totals = OrderPricing.CalculateTotals(OrderWith(12.00m, NycTax)); // 1.065

        Assert.Equal(Money.Usd(1.07m), totals.Tax);
        Assert.Equal(Money.Usd(13.07m), totals.Total);
    }

    [Fact]
    public void Tax_base_is_the_subtotal_after_discount()
    {
        var order = OrderWith(12.00m, NycTax);

        order.DiscountAmount = Money.Usd(2.00m); // tax base 10.00 → 0.8875

        var totals = OrderPricing.CalculateTotals(order);
        Assert.Equal(Money.Usd(0.89m), totals.Tax);
        Assert.Equal(Money.Usd(10.89m), totals.Total);
    }

    [Fact]
    public void Tip_is_rounded_away_from_zero_at_the_midpoint()
    {
        var order = OrderWith(12.30m, Percentage.Zero);

        order.TipRate = new Percentage(15m); // 1.845

        var totals = OrderPricing.CalculateTotals(order);
        Assert.Equal(Money.Usd(1.85m), totals.Tip);
        Assert.Equal(Money.Usd(14.15m), totals.Total);
    }

    [Fact]
    public void Tip_base_is_pre_tax_so_the_tax_rate_does_not_change_the_tip()
    {
        var untaxed = OrderWith(12.00m, Percentage.Zero);
        var taxed = OrderWith(12.00m, NycTax);
        untaxed.TipRate = new Percentage(18m);
        taxed.TipRate = new Percentage(18m);

        Assert.Equal(Money.Usd(2.16m), OrderPricing.CalculateTotals(untaxed).Tip);
        Assert.Equal(Money.Usd(2.16m), OrderPricing.CalculateTotals(taxed).Tip);
        Assert.Equal(Money.Usd(15.23m), OrderPricing.CalculateTotals(taxed).Total); // 12.00 + 1.07 + 2.16
    }

    [Fact]
    public void Tip_base_is_the_subtotal_after_discount()
    {
        var order = OrderWith(12.00m, Percentage.Zero);
        order.TipRate = new Percentage(18m);

        order.DiscountAmount = Money.Usd(2.00m); // tip base 10.00

        Assert.Equal(Money.Usd(1.80m), OrderPricing.CalculateTotals(order).Tip);
    }

    [Fact]
    public void Each_step_is_rounded_before_the_next_one_uses_it()
    {
        var order = OrderWith(5.00m, NycTax);
        order.DiscountRate = new Percentage(15m);
        order.TipRate = new Percentage(18m);

        var totals = OrderPricing.CalculateTotals(order);

        // discount 0.75 → base 4.25; tax 0.3771875 → 0.38; tip 0.765 → 0.77.
        // Rounding only the final total would give 5.39 (4.25 + 0.3771875 + 0.765 = 5.3921875).
        Assert.Equal(Money.Usd(5.00m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.75m), totals.Discount);
        Assert.Equal(Money.Usd(0.38m), totals.Tax);
        Assert.Equal(Money.Usd(0.77m), totals.Tip);
        Assert.Equal(Money.Usd(5.40m), totals.Total);
    }
}
