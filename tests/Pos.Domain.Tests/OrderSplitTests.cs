using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>
/// Split by item: each part gets whole lines; the order's discount, surcharge, tax and tip are each shared by the
/// part's share of the subtotal, rounded down to the cent, with the leftover cents going to the part with the largest
/// subtotal (the earlier part on a tie). Every amount of the parts must add up exactly to the order's amount.
/// </summary>
public class OrderSplitTests
{
    private static readonly Percentage NycTax = new(8.875m);
    private static readonly Percentage CardSurcharge = new(3m);

    private static (Order Order, Guid[] LineIds) OrderWithLines(Percentage taxRate, params decimal[] prices) =>
        TestOrders.WithLines(taxRate, CardSurcharge, prices);

    private static Guid[] Part(params Guid[] lineIds) => lineIds;

    [Fact]
    public void Leftover_cents_go_to_the_earlier_part_on_a_tie()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 3.33m, 3.33m); // tax 8.875% of 9.99 = 0.8866 → 0.89

        var parts = OrderSplitting.Split(order, [Part(l[0]), Part(l[1]), Part(l[2])]).Data;

        // 0.89 / 3 = 0.2966 → 0.29 each, leftover 0.02.
        Assert.Equal([Money.Usd(0.31m), Money.Usd(0.29m), Money.Usd(0.29m)], parts.Select(p => p.Tax));
        Assert.Equal([Money.Usd(3.64m), Money.Usd(3.62m), Money.Usd(3.62m)], parts.Select(p => p.Total));
    }

    [Fact]
    public void Leftover_cent_goes_to_the_part_with_the_largest_subtotal()
    {
        var (order, l) = OrderWithLines(NycTax, 1.00m, 2.33m); // tax 8.875% of 3.33 = 0.2955 → 0.30

        var parts = OrderSplitting.Split(order, [Part(l[0]), Part(l[1])]).Data;

        // 0.30 × 1.00/3.33 = 0.0900 → 0.09; 0.30 × 2.33/3.33 = 0.2099 → 0.20; leftover 0.01 to the 2.33 part.
        Assert.Equal([Money.Usd(0.09m), Money.Usd(0.21m)], parts.Select(p => p.Tax));
        Assert.Equal([Money.Usd(1.09m), Money.Usd(2.54m)], parts.Select(p => p.Total));
    }

    [Fact]
    public void A_share_on_the_half_cent_rounds_down_and_no_part_goes_negative()
    {
        var (order, l) = OrderWithLines(NycTax, 0.06m, 0.06m, 0.06m, 0.06m); // tax 8.875% of 0.24 = 0.0213 → 0.02

        var parts = OrderSplitting.Split(order, [Part(l[0]), Part(l[1]), Part(l[2]), Part(l[3])]).Data;

        // 0.02 / 4 = 0.005 → 0.00 each, leftover 0.02 to the first part (tie). Rounding half up would give 0.01 each
        // and a leftover of −0.01: taxes [−0.01, 0.01, 0.01, 0.01].
        Assert.Equal([Money.Usd(0.02m), Money.Usd(0.00m), Money.Usd(0.00m), Money.Usd(0.00m)], parts.Select(p => p.Tax));
        Assert.Equal([Money.Usd(0.08m), Money.Usd(0.06m), Money.Usd(0.06m), Money.Usd(0.06m)], parts.Select(p => p.Total));
        AssertNoNegativeAmounts(parts);
    }

    [Fact]
    public void A_half_cent_share_of_a_rounded_up_tax_also_rounds_down()
    {
        var (order, l) = OrderWithLines(NycTax, 0.05m, 0.05m, 0.05m, 0.05m); // tax 8.875% of 0.20 = 0.01775 → 0.02

        var parts = OrderSplitting.Split(order, [Part(l[0]), Part(l[1]), Part(l[2]), Part(l[3])]).Data;

        // 0.02 / 4 = 0.005 → 0.00 each, leftover 0.02 to the first part; half up would leave the first part −0.01.
        Assert.Equal([Money.Usd(0.02m), Money.Usd(0.00m), Money.Usd(0.00m), Money.Usd(0.00m)], parts.Select(p => p.Tax));
        Assert.Equal([Money.Usd(0.07m), Money.Usd(0.05m), Money.Usd(0.05m), Money.Usd(0.05m)], parts.Select(p => p.Total));
        AssertNoNegativeAmounts(parts);
    }

    [Fact]
    public void Discount_surcharge_tax_and_tip_are_each_shared_separately()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 3.33m, 3.33m);
        order.DiscountRate = new Percentage(15m);
        order.PaymentMethod = PaymentMethod.Card;
        order.TipRate = new Percentage(18m);
        // Order: subtotal 9.99, discount 1.50, surcharge 0.25, tax 0.78, tip 1.53, total 11.05.

        var parts = OrderSplitting.Split(order, [Part(l[0]), Part(l[1]), Part(l[2])]).Data;

        Assert.All(parts, p => Assert.Equal(Money.Usd(0.50m), p.Discount));
        Assert.Equal([Money.Usd(0.09m), Money.Usd(0.08m), Money.Usd(0.08m)], parts.Select(p => p.Surcharge));
        Assert.All(parts, p => Assert.Equal(Money.Usd(0.26m), p.Tax));
        Assert.All(parts, p => Assert.Equal(Money.Usd(0.51m), p.Tip));
        Assert.Equal([Money.Usd(3.69m), Money.Usd(3.68m), Money.Usd(3.68m)], parts.Select(p => p.Total));
        Assert.Equal(Money.Usd(11.05m), OrderPricing.CalculateTotals(order).Total);
    }

    public static TheoryData<int, bool, bool> PricingCombinations()
    {
        var data = new TheoryData<int, bool, bool>();
        foreach (var discountKind in new[] { 0, 1, 2 })
        {
            foreach (var card in new[] { false, true })
            {
                foreach (var tip in new[] { false, true })
                {
                    data.Add(discountKind, card, tip);
                }
            }
        }

        return data;
    }

    /// <param name="discountKind">0 = none, 1 = 15% of subtotal, 2 = fixed 1.00.</param>
    /// <param name="card">Pay by card, so the order has a surcharge.</param>
    /// <param name="tip">Add an 18% tip.</param>
    [Theory]
    [MemberData(nameof(PricingCombinations))]
    public void Parts_add_up_exactly_to_the_order(int discountKind, bool card, bool tip)
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 4.17m, 2.51m, 15.40m, 0.99m);
        if (discountKind == 1)
        {
            order.DiscountRate = new Percentage(15m);
        }
        else if (discountKind == 2)
        {
            order.DiscountAmount = Money.Usd(1.00m);
        }

        order.PaymentMethod = card ? PaymentMethod.Card : PaymentMethod.Cash;
        if (tip)
        {
            order.TipRate = new Percentage(18m);
        }

        var parts = OrderSplitting.Split(order, [Part(l[0], l[2]), Part(l[1]), Part(l[3], l[4])]).Data;

        var totals = OrderPricing.CalculateTotals(order);
        Money Sum(Func<SplitPart, Money> amount) => parts.Aggregate(Money.Usd(0m), (sum, p) => sum + amount(p));
        Assert.Equal(totals.Subtotal, Sum(p => p.Subtotal));
        Assert.Equal(totals.Discount, Sum(p => p.Discount));
        Assert.Equal(totals.Surcharge, Sum(p => p.Surcharge));
        Assert.Equal(totals.Tax, Sum(p => p.Tax));
        Assert.Equal(totals.Tip, Sum(p => p.Tip));
        Assert.Equal(totals.Total, Sum(p => p.Total));
        AssertNoNegativeAmounts(parts);
    }

    private static void AssertNoNegativeAmounts(IEnumerable<SplitPart> parts) =>
        Assert.All(parts, p => Assert.All(
            [p.Subtotal, p.Discount, p.Surcharge, p.Tax, p.Tip, p.Total],
            amount => Assert.True(amount.Amount >= 0m, $"Negative part amount {amount.Amount}.")));

    [Fact]
    public void One_part_is_rejected()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 4.17m);

        var error = OrderSplitting.Split(order, [Part(l[0], l[1])]).Error;

        Assert.Equal("A split needs at least two parts.", error.Message);
    }

    [Fact]
    public void Every_line_must_be_in_a_part()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 4.17m, 2.51m);

        var error = OrderSplitting.Split(order, [Part(l[0]), Part(l[1])]).Error;

        Assert.Equal("Every line of the order must be in a part of the split.", error.Message);
    }

    [Fact]
    public void A_line_cannot_be_in_two_parts()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m, 4.17m);

        Assert.Equal(ErrorCode.InvalidSplit, OrderSplitting.Split(order, [Part(l[0], l[1]), Part(l[1])]).Error.Code);
    }

    [Fact]
    public void A_line_from_another_order_is_rejected()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m);

        Assert.Equal(ErrorCode.LineNotOnOrder, OrderSplitting.Split(order, [Part(l[0]), Part(Guid.NewGuid())]).Error.Code);
    }

    [Fact]
    public void An_empty_part_is_rejected()
    {
        var (order, l) = OrderWithLines(NycTax, 3.33m);

        Assert.Equal(ErrorCode.InvalidSplit, OrderSplitting.Split(order, [Part(l[0]), Part()]).Error.Code);
    }

    [Fact]
    public void A_split_needs_at_least_two_parts()
    {
        var (order, _) = OrderWithLines(NycTax, 3.33m);

        var error = OrderSplitting.Split(order, []).Error;

        Assert.Equal("A split needs at least two parts.", error.Message);
    }
}
