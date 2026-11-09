using Pos.Application.Orders;
using Pos.Application.Orders.CreateOrder;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class CreateOrderHandlerTests
{
    private readonly OrderActions _act = new();

    [Fact]
    public async Task New_order_is_open_empty_paid_in_cash_and_snapshots_the_rates()
    {
        _act.Time.Now = new DateTimeOffset(2026, 10, 26, 14, 30, 0, TimeSpan.FromHours(7));
        var handler = new CreateOrderHandler(_act.Orders, _act.Time,
            new PricingOptions { TaxRatePercent = 8.875m, CardSurchargePercent = 3m }, _act.Logger<CreateOrderHandler>());

        var result = (await handler.HandleAsync(new CreateOrderCommand(), CancellationToken.None)).Data!;

        var order = Assert.Single(_act.Orders.Orders.Values);
        Assert.Equal(order.Id, result.Id);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(_act.Time.Now, order.CreatedAt);
        Assert.Equal(TimeSpan.Zero, order.CreatedAt.Offset);
        Assert.Equal(Currency.USD, order.Currency);
        Assert.Equal(new Percentage(8.875m), order.TaxRate);
        Assert.Equal(new Percentage(3m), order.CardSurchargeRate);
        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
        Assert.Empty(order.Lines);
        Assert.Equal(Money.Usd(0.00m), result.Subtotal);
        Assert.Equal(Money.Usd(0.00m), result.Total);
        Assert.Equal(1, _act.Orders.SaveCount);
    }
}
