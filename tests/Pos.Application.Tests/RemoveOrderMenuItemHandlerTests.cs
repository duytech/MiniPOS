using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class RemoveOrderMenuItemHandlerTests
{
    private readonly OrderActions _act = new();

    [Fact]
    public async Task Removing_an_item_keeps_the_other_items()
    {
        var order = _act.NewOrder();
        var burger = await _act.AddPricedItem(order, 12.99m, 2);
        var fries = await _act.AddPricedItem(order, 3.35m);

        var result = (await _act.RemoveItem(order, burger)).Data!;

        Assert.Equal(fries, Assert.Single(order.Items).Id);
        Assert.Equal(Money.Usd(3.35m), result.Total);
    }

    [Fact]
    public async Task Removing_the_last_item_leaves_the_order_open_and_empty()
    {
        var order = _act.NewOrder();
        var burger = await _act.AddPricedItem(order, 12.99m);

        var result = (await _act.RemoveItem(order, burger)).Data!;

        Assert.Empty(order.Items);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal(Money.Usd(0.00m), result.Total);
    }

    [Fact]
    public async Task Unknown_item_is_not_found_and_nothing_is_saved()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 12.99m);
        var savesBefore = _act.Orders.SaveCount;

        var result = await _act.RemoveItem(order, Guid.NewGuid());

        Assert.Equal(ErrorCode.OrderItemNotFound, result.Error!.Code);
        Assert.Single(order.Items);
        Assert.Equal(savesBefore, _act.Orders.SaveCount);
    }

    [Fact]
    public async Task Percent_discount_follows_the_subtotal_after_an_item_is_removed_and_rounds_away_from_zero()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 0.70m);
        var tea = await _act.AddPricedItem(order, 2.00m);
        var discounted = (await _act.ApplyPercentDiscount(order, 15m)).Data!;
        Assert.Equal(Money.Usd(0.41m), discounted.Discount); // 15% of 2.70 = 0.405

        var totals = (await _act.RemoveItem(order, tea)).Data!;

        Assert.Equal(Money.Usd(0.70m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.11m), totals.Discount); // 15% of 0.70 = 0.105
        Assert.Equal(Money.Usd(0.59m), totals.Total);
    }

    [Fact]
    public async Task Fixed_discount_equal_to_the_new_subtotal_allows_removing_an_item()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 7.70m);
        var tea = await _act.AddPricedItem(order, 2.00m);
        await _act.ApplyFixedDiscount(order, 7.70m);

        var totals = (await _act.RemoveItem(order, tea)).Data!;

        Assert.Equal(Money.Usd(7.70m), totals.Discount);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public async Task Fixed_discount_more_than_the_new_subtotal_rejects_removing_an_item()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 7.70m);
        var tea = await _act.AddPricedItem(order, 2.00m);
        await _act.ApplyFixedDiscount(order, 7.71m);

        var result = await _act.RemoveItem(order, tea);

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, result.Error!.Code);
        Assert.Equal(
            "Removing the item would leave a subtotal of 7.70 USD, less than the discount of 7.71 USD. " +
            "Change or remove the discount first.",
            result.Error!.Message);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(Money.Usd(1.99m), OrderPricing.CalculateTotals(order).Total);
    }
}
