using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>
/// Card surcharge: card surcharge rate × (subtotal − discount), charged only when paying by card, rounded to cents
/// (AwayFromZero). Bases: the surcharge is taxed (tax on subtotal − discount + surcharge) but not tipped
/// (tip on subtotal − discount).
/// </summary>
public class OrderSurchargeTests
{
    private static readonly Percentage NycTax = new(8.875m);
    private static readonly Percentage CardSurcharge = new(3m);

    private static Order OrderWith(decimal subtotal, Percentage taxRate, PaymentMethod paymentMethod)
    {
        var order = Order.Create(DateTimeOffset.UtcNow, taxRate, CardSurcharge);
        order.AddItem(new MenuItem("Item", Money.Usd(subtotal)), 1, notes: null);
        order.SetPaymentMethod(paymentMethod);
        return order;
    }

    [Fact]
    public void New_order_is_paid_in_cash()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, NycTax, CardSurcharge);

        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
        Assert.Equal(CardSurcharge, order.CardSurchargeRate);
    }

    [Fact]
    public void Card_surcharge_is_rounded_away_from_zero_at_the_midpoint()
    {
        var order = OrderWith(11.50m, Percentage.Zero, PaymentMethod.Card); // 3% of 11.50 = 0.345

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.35m), totals.Surcharge);
        Assert.Equal(Money.Usd(11.85m), totals.Total);
    }

    [Fact]
    public void Cash_has_no_surcharge()
    {
        var order = OrderWith(11.50m, Percentage.Zero, PaymentMethod.Cash);

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.00m), totals.Surcharge);
        Assert.Equal(Money.Usd(11.50m), totals.Total);
    }

    [Fact]
    public void Switching_back_to_cash_removes_the_surcharge()
    {
        var order = OrderWith(11.50m, Percentage.Zero, PaymentMethod.Card);

        order.SetPaymentMethod(PaymentMethod.Cash);

        Assert.Equal(Money.Usd(0.00m), order.CalculateTotals().Surcharge);
    }

    [Fact]
    public void Surcharge_base_is_subtotal_after_discount()
    {
        var order = OrderWith(12.00m, Percentage.Zero, PaymentMethod.Card);

        order.ApplyDiscount(OrderDiscount.FixedAmount(Money.Usd(2.00m)));

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.30m), totals.Surcharge); // 3% of 10.00; on the subtotal it would be 0.36
        Assert.Equal(Money.Usd(10.30m), totals.Total);
    }

    [Fact]
    public void Surcharge_is_taxed()
    {
        var order = OrderWith(10.00m, NycTax, PaymentMethod.Card);

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(0.30m), totals.Surcharge);
        Assert.Equal(Money.Usd(0.91m), totals.Tax); // 8.875% of 10.30 = 0.914125; of 10.00 it would be 0.89
        Assert.Equal(Money.Usd(11.21m), totals.Total);
    }

    [Fact]
    public void Surcharge_is_not_tipped()
    {
        var order = OrderWith(10.00m, Percentage.Zero, PaymentMethod.Card);

        order.SetTip(new Percentage(18m));

        var totals = order.CalculateTotals();
        Assert.Equal(Money.Usd(1.80m), totals.Tip); // 18% of 10.00; of 10.30 it would be 1.85
        Assert.Equal(Money.Usd(12.10m), totals.Total);
    }

    [Fact]
    public void Each_step_is_rounded_before_the_next_one_uses_it()
    {
        var order = OrderWith(5.00m, NycTax, PaymentMethod.Card);
        order.ApplyDiscount(OrderDiscount.PercentOfSubtotal(new Percentage(15m)));
        order.SetTip(new Percentage(18m));

        var totals = order.CalculateTotals();

        Assert.Equal(Money.Usd(0.75m), totals.Discount);
        Assert.Equal(Money.Usd(0.13m), totals.Surcharge); // 3% of 4.25 = 0.1275
        Assert.Equal(Money.Usd(0.39m), totals.Tax); // 8.875% of 4.38 = 0.388725
        Assert.Equal(Money.Usd(0.77m), totals.Tip); // 18% of 4.25 = 0.765
        // Rounding only at the end: 4.25 + 0.1275 + 0.388503125 + 0.765 = 5.531003125, i.e. 5.53.
        Assert.Equal(Money.Usd(5.54m), totals.Total);
    }
}
