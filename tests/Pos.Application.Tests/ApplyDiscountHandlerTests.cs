using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

/// <summary>Applying and removing the order discount (ApplyDiscountHandler, RemoveDiscountHandler).</summary>
public class ApplyDiscountHandlerTests
{
    private readonly OrderActions _act = new();

    private async Task<Order> OrderWith(decimal subtotal)
    {
        var order = _act.NewOrder();
        await _act.AddLine(order, subtotal);
        return order;
    }

    [Fact]
    public async Task Fixed_discount_above_subtotal_is_rejected_and_not_stored()
    {
        var order = await OrderWith(11.50m);

        var result = await _act.ApplyFixedDiscount(order, 11.51m);

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, result.Error.Code);
        Assert.Equal("A discount of 11.51 USD is more than the subtotal of 11.50 USD.", result.Error.Message);
        Assert.Null(OrderPricing.Discount(order));
    }

    [Fact]
    public async Task Percent_discount_above_100_is_rejected()
    {
        var order = await OrderWith(11.50m);

        var result = await _act.ApplyPercentDiscount(order, 100.01m);

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, result.Error.Code);
        Assert.Null(OrderPricing.Discount(order));
    }

    [Fact]
    public async Task Applying_a_discount_replaces_the_previous_one_and_it_can_be_removed()
    {
        var order = await OrderWith(10.00m);
        await _act.ApplyFixedDiscount(order, 2.00m);

        var replaced = (await _act.ApplyPercentDiscount(order, 10m)).Data;
        Assert.Equal(Money.Usd(1.00m), replaced.Discount);
        Assert.Null(order.DiscountAmount);

        var removed = (await _act.RemoveDiscount(order)).Data;
        Assert.Null(OrderPricing.Discount(order));
        Assert.Equal(Money.Usd(10.00m), removed.Total);
    }
}
