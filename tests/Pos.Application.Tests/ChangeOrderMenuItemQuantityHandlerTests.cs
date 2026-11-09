using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class ChangeOrderMenuItemQuantityHandlerTests
{
    private readonly OrderActions _act = new();

    [Fact]
    public async Task Changing_an_item_quantity_replaces_it_and_keeps_the_other_items()
    {
        var order = _act.NewOrder();
        var burger = await _act.AddPricedItem(order, 12.99m, 2);
        var fries = await _act.AddPricedItem(order, 3.35m);

        var raised = (await _act.ChangeQuantity(order, burger, 5)).Data!;

        Assert.Equal([5, 1], order.Items.Select(l => l.Quantity));
        Assert.Equal([burger, fries], order.Items.Select(l => l.Id));
        Assert.Equal(Money.Usd(68.30m), raised.Total); // 12.99 × 5 + 3.35

        var lowered = (await _act.ChangeQuantity(order, burger, 1)).Data!;

        Assert.Equal(1, order.Items[0].Quantity);
        Assert.Equal(Money.Usd(16.34m), lowered.Total);
    }

    [Fact]
    public async Task Changing_an_item_to_its_current_quantity_leaves_the_order_unchanged()
    {
        var order = _act.NewOrder();
        var burger = await _act.AddPricedItem(order, 12.99m, 2);

        var result = (await _act.ChangeQuantity(order, burger, 2)).Data!;

        Assert.Equal(2, Assert.Single(order.Items).Quantity);
        Assert.Equal(Money.Usd(25.98m), result.Total);
    }

    [Fact]
    public async Task Unknown_item_is_not_found_and_nothing_is_saved()
    {
        var order = _act.NewOrder();
        await _act.AddPricedItem(order, 12.99m);
        var savesBefore = _act.Orders.SaveCount;

        var result = await _act.ChangeQuantity(order, Guid.NewGuid(), 3);

        Assert.Equal(ErrorCode.OrderItemNotFound, result.Error!.Code);
        Assert.Equal(1, Assert.Single(order.Items).Quantity);
        Assert.Equal(savesBefore, _act.Orders.SaveCount);
    }

    [Fact]
    public async Task Percent_discount_and_tip_follow_the_subtotal_after_a_quantity_change_and_round_away_from_zero()
    {
        var order = _act.NewOrder();
        var tea = await _act.AddPricedItem(order, 0.35m, 4);
        var discounted = (await _act.ApplyPercentDiscount(order, 15m)).Data!;
        await _act.SetTip(order, 10m);
        Assert.Equal(Money.Usd(0.21m), discounted.Discount); // 15% of 1.40

        var totals = (await _act.ChangeQuantity(order, tea, 2)).Data!;

        Assert.Equal(Money.Usd(0.70m), totals.Subtotal);
        Assert.Equal(Money.Usd(0.11m), totals.Discount); // 15% of 0.70 = 0.105
        Assert.Equal(Money.Usd(0.06m), totals.Tip); // 10% of tip base 0.59 = 0.059
        Assert.Equal(Money.Usd(0.65m), totals.Total);
    }

    [Fact]
    public async Task Fixed_discount_equal_to_the_new_subtotal_allows_lowering_a_quantity()
    {
        var order = _act.NewOrder();
        var pho = await _act.AddPricedItem(order, 3.85m, 3);
        await _act.ApplyFixedDiscount(order, 7.70m);

        var totals = (await _act.ChangeQuantity(order, pho, 2)).Data!;

        Assert.Equal(Money.Usd(7.70m), totals.Subtotal);
        Assert.Equal(Money.Usd(7.70m), totals.Discount);
        Assert.Equal(Money.Usd(0.00m), totals.Total);
    }

    [Fact]
    public async Task Fixed_discount_more_than_the_new_subtotal_rejects_lowering_a_quantity()
    {
        var order = _act.NewOrder();
        var pho = await _act.AddPricedItem(order, 3.85m, 3);
        await _act.ApplyFixedDiscount(order, 7.71m);

        var result = await _act.ChangeQuantity(order, pho, 2);

        Assert.Equal(ErrorCode.DiscountExceedsSubtotal, result.Error!.Code);
        Assert.StartsWith("A quantity of 2 would leave a subtotal of 7.70 USD", result.Error!.Message);
        Assert.Equal(3, order.Items[0].Quantity);
        Assert.Equal(Money.Usd(3.84m), OrderPricing.CalculateTotals(order).Total); // 11.55 − 7.71
    }

    [Fact]
    public async Task Fixed_discount_does_not_block_raising_a_quantity()
    {
        var order = _act.NewOrder();
        var pho = await _act.AddPricedItem(order, 3.85m, 2);
        await _act.ApplyFixedDiscount(order, 7.70m);

        var totals = (await _act.ChangeQuantity(order, pho, 3)).Data!;

        Assert.Equal(Money.Usd(3.85m), totals.Total); // 11.55 − 7.70
    }
}
