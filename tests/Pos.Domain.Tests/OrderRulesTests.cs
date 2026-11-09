using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

public class OrderRulesTests
{
    [Fact]
    public void An_open_order_can_change()
    {
        Assert.Null(OrderRules.OpenError(TestOrders.New()));
    }

    [Theory]
    [InlineData(OrderStatus.Paid)]
    [InlineData(OrderStatus.Cancelled)]
    public void A_closed_order_cannot_change(OrderStatus status)
    {
        var order = TestOrders.New();
        order.Status = status;

        var error = OrderRules.OpenError(order);

        Assert.NotNull(error);
        Assert.Equal(ErrorCode.OrderNotOpen, error.Code);
        Assert.Equal($"The order is {status}; only an open order can be changed.", error.Message);
    }

    [Fact]
    public void No_fixed_discount_or_one_up_to_the_subtotal_is_allowed()
    {
        Assert.Null(OrderRules.FixedDiscountError(null, Money.Usd(0.00m), change: null));
        Assert.Null(OrderRules.FixedDiscountError(Money.Usd(7.70m), Money.Usd(7.70m), change: null));
        Assert.Null(OrderRules.FixedDiscountError(Money.Usd(7.69m), Money.Usd(7.70m), "Removing the item"));
    }

    [Fact]
    public void A_fixed_discount_above_the_subtotal_names_the_change_that_would_cause_it()
    {
        var applying = OrderRules.FixedDiscountError(Money.Usd(7.71m), Money.Usd(7.70m), change: null);
        var removing = OrderRules.FixedDiscountError(Money.Usd(7.71m), Money.Usd(7.70m), "Removing the item");

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, applying!.Code);
        Assert.Equal("A discount of 7.71 USD is more than the subtotal of 7.70 USD.", applying.Message);
        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, removing!.Code);
        Assert.Equal(
            "Removing the item would leave a subtotal of 7.70 USD, less than the discount of 7.71 USD. " +
            "Change or remove the discount first.",
            removing.Message);
    }
}
