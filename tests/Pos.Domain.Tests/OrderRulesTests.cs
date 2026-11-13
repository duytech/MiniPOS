using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderRulesTests
{
    [Fact]
    public void An_open_order_can_change()
    {
        Assert.True(OrderRules.IsOpen(TestOrders.New()));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void A_closed_order_cannot_change(OrderStatus status)
    {
        var order = TestOrders.New();
        order.Status = status;

        Assert.False(OrderRules.IsOpen(order));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void The_not_open_error_names_the_status(OrderStatus status)
    {
        var error = OrderErrors.NotOpen(status);

        Assert.Equal(ErrorCode.OrderNotOpen, error.Code);
        Assert.Equal($"The order is {status}; only an open order can be changed.", error.Message);
    }

    [Fact]
    public void A_fixed_discount_up_to_the_subtotal_fits()
    {
        Assert.True(OrderRules.CanFitFixedDiscount(Money.Usd(7.70m), Money.Usd(7.70m)));
        Assert.True(OrderRules.CanFitFixedDiscount(Money.Usd(7.69m), Money.Usd(7.70m)));
        Assert.False(OrderRules.CanFitFixedDiscount(Money.Usd(7.71m), Money.Usd(7.70m)));
    }

    [Fact]
    public void A_fixed_discount_above_the_subtotal_names_the_change_that_would_cause_it()
    {
        var applying = OrderErrors.DiscountExceedsSubtotal(Money.Usd(7.71m), Money.Usd(7.70m));
        var removing = OrderErrors.DiscountExceedsNewSubtotal("Removing the item", Money.Usd(7.71m), Money.Usd(7.70m));

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, applying.Code);
        Assert.Equal("A discount of 7.71 USD is more than the subtotal of 7.70 USD.", applying.Message);
        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, removing.Code);
        Assert.Equal(
            "Removing the item would leave a subtotal of 7.70 USD, less than the discount of 7.71 USD. " +
            "Change or remove the discount first.",
            removing.Message);
    }
}
