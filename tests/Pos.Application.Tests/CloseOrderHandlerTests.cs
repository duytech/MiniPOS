using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

/// <summary>
/// PayOrderHandler and CancelOrderHandler, plus the open-only rule every changing handler checks: Paid and Cancelled
/// are final.
/// </summary>
public class CloseOrderHandlerTests
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 10, 27, 9, 15, 0, TimeSpan.Zero);
    private static readonly MenuItem Burger = OrderActions.Item("Burger", 12.99m);

    private readonly OrderActions _act = new();

    public static TheoryData<OrderStatus, string> ClosedOrderChanges()
    {
        var data = new TheoryData<OrderStatus, string>();
        foreach (var status in new[] { OrderStatus.Paid, OrderStatus.Cancelled })
        {
            foreach (var change in new[]
                     {
                         "remove-item", "change-quantity", "add-item", "apply-percent-discount", "apply-fixed-discount",
                         "remove-discount", "set-tip", "remove-tip", "set-payment-method",
                     })
            {
                data.Add(status, change);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ClosedOrderChanges))]
    public async Task Items_discount_tip_and_payment_method_cannot_change_once_the_order_is_closed(
        OrderStatus status, string change)
    {
        var order = _act.NewOrder();
        await _act.AddItem(order, Burger, 1);
        var totalsBefore = OrderPricing.CalculateTotals(order);
        await _act.Close(order, status);
        var savesBefore = _act.Orders.SaveCount;

        var result = change switch
        {
            "remove-item" => await _act.RemoveItem(order, order.Items[0].Id),
            "change-quantity" => await _act.ChangeQuantity(order, order.Items[0].Id, 3),
            "add-item" => await _act.AddItem(order, Burger, 1),
            "apply-percent-discount" => await _act.ApplyPercentDiscount(order, 10m),
            "apply-fixed-discount" => await _act.ApplyFixedDiscount(order, 1.00m),
            "remove-discount" => await _act.RemoveDiscount(order),
            "set-tip" => await _act.SetTip(order, 15m),
            "remove-tip" => await _act.RemoveTip(order),
            _ => await _act.SetPaymentMethod(order, PaymentMethod.Card),
        };

        Assert.Equal(ErrorCode.OrderNotOpen, result.Error!.Code);
        Assert.Equal($"The order is {status}; only an open order can be changed.", result.Error!.Message);
        Assert.Equal(1, Assert.Single(order.Items).Quantity);
        Assert.Equal(totalsBefore, OrderPricing.CalculateTotals(order));
        Assert.Equal(PaymentMethod.Cash, order.PaymentMethod);
        Assert.Equal(savesBefore, _act.Orders.SaveCount);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Closing_an_open_order_sets_the_status_and_its_time_and_keeps_the_totals(OrderStatus status)
    {
        var order = _act.NewOrder(new Percentage(8.875m), new Percentage(3m));
        await _act.AddPricedItem(order, 0.70m);
        await _act.ApplyPercentDiscount(order, 15m);
        await _act.SetTip(order, 15m);
        await _act.SetPaymentMethod(order, PaymentMethod.Card);
        var totalsBefore = OrderPricing.CalculateTotals(order);

        var totals = (await _act.Close(order, status, ClosedAt)).Data!;

        Assert.Equal(status, order.Status);
        Assert.Equal(status == OrderStatus.Paid ? ClosedAt : null, order.PaidAt);
        Assert.Equal(status == OrderStatus.Cancelled ? ClosedAt : null, order.CancelledAt);
        Assert.Equal(totalsBefore, OrderPricing.CalculateTotals(order));
        Assert.Equal(Money.Usd(0.70m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.11m), totals.Discount); // 15% of 0.70 = 0.105, midpoint away from zero
        Assert.Equal(Money.Usd(0.02m), totals.Surcharge); // 3% of 0.59 = 0.0177
        Assert.Equal(Money.Usd(0.05m), totals.Tax); // 8.875% of 0.61 = 0.0541375
        Assert.Equal(Money.Usd(0.09m), totals.Tip); // 15% of 0.59 = 0.0885
        Assert.Equal(Money.Usd(0.75m), totals.Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task An_empty_order_can_be_closed(OrderStatus status)
    {
        var order = _act.NewOrder(new Percentage(8.875m));

        var result = (await _act.Close(order, status)).Data!;

        Assert.Equal(status, order.Status);
        Assert.Empty(order.Items);
        Assert.Equal(Money.Usd(0.00m), result.Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid, OrderStatus.Paid)]
    [InlineData(OrderStatus.Paid, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Paid)]
    public async Task A_closed_order_cannot_be_paid_or_cancelled_again(OrderStatus first, OrderStatus second)
    {
        var order = _act.NewOrder();
        await _act.AddItem(order, Burger, 1);
        await _act.Close(order, first, ClosedAt);
        var (paidAt, cancelledAt) = (order.PaidAt, order.CancelledAt);

        var result = await _act.Close(order, second, ClosedAt.AddMinutes(5));

        Assert.Equal(ErrorCode.OrderNotOpen, result.Error!.Code);
        Assert.Equal(first, order.Status);
        Assert.Equal(paidAt, order.PaidAt);
        Assert.Equal(cancelledAt, order.CancelledAt);
        Assert.Equal(Money.Usd(12.99m), OrderPricing.CalculateTotals(order).Total);
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task The_closing_time_is_kept_in_utc(OrderStatus status)
    {
        var order = _act.NewOrder();
        var closedAt = new DateTimeOffset(2026, 10, 27, 16, 15, 0, TimeSpan.FromHours(7));

        await _act.Close(order, status, closedAt);

        var stored = status == OrderStatus.Paid ? order.PaidAt : order.CancelledAt;
        Assert.Equal(closedAt, stored);
        Assert.Equal(TimeSpan.Zero, stored!.Value.Offset);
    }
}
