using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

/// <summary>SetTipHandler, RemoveTipHandler and SetPaymentMethodHandler.</summary>
public class TipAndPaymentMethodHandlerTests
{
    private readonly OrderActions _act = new();

    [Fact]
    public async Task Removing_the_tip_sets_it_to_zero()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 12.00m);
        var tipped = (await _act.SetTip(order, 18m)).Data!;
        Assert.Equal(Money.Usd(2.16m), tipped.Tip);

        var result = (await _act.RemoveTip(order)).Data!;

        Assert.Null(order.TipRate);
        Assert.Equal(Money.Usd(0.00m), result.Tip);
    }

    [Fact]
    public async Task Switching_back_to_cash_removes_the_surcharge()
    {
        var order = _act.NewOrder(cardSurchargeRate: new Percentage(3m));
        await _act.AddPricedItem(order, 11.50m);
        var card = (await _act.SetPaymentMethod(order, PaymentMethod.Card)).Data!;
        Assert.Equal(Money.Usd(0.35m), card.Surcharge);

        var cash = (await _act.SetPaymentMethod(order, PaymentMethod.Cash)).Data!;

        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
        Assert.Equal(Money.Usd(0.00m), cash.Surcharge);
    }
}
