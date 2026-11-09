using System.Net.Http.Json;
using System.Text.Json;
using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>PUT /api/orders/{id}/items/{itemId}/quantity. Pricing config: tax 8.875%, card surcharge 3%.</summary>
public class ChangeOrderMenuItemQuantityApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Changing_a_quantity_returns_the_repriced_order()
    {
        var (orderId, phoItem, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeItemQuantityAsync(orderId, teaItem, 3);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal(3, QuantityOf(order, teaItem));
        Assert.Equal(1, QuantityOf(order, phoItem));
        Assert.Equal("16.00", order.Money("subtotal"));
        Assert.Equal("1.42", order.Money("tax")); // 8.875% of 16.00 = 1.42
        Assert.Equal("17.42", order.Money("total"));
        Assert.Equal("17.42", (await _client.GetOrderAsync(orderId)).Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Percent_discount_follows_the_new_subtotal_and_midpoint_rounds_away_from_zero()
    {
        var (orderId, phoItem, _) = await OrderWithPhoAndTeaAsync(0.35m, 1, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).AssertSuccessAsync();

        var order = await (await _client.ChangeItemQuantityAsync(orderId, phoItem, 2)).ReadDataAsync();

        Assert.Equal("2.70", order.Money("subtotal"));
        Assert.Equal("0.41", order.Money("discount")); // 15% of 2.70 = 0.405
        Assert.Equal("0.20", order.Money("tax")); // 8.875% of 2.29 = 0.2032375
        Assert.Equal("2.49", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Fixed_discount_more_than_the_new_subtotal_is_rejected_and_leaves_the_order_unchanged()
    {
        var (orderId, phoItem, _) = await OrderWithPhoAndTeaAsync(3.85m, 3, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { amount = 9.71m })).AssertSuccessAsync();

        var response = await _client.ChangeItemQuantityAsync(orderId, phoItem, 2); // subtotal would be 9.70

        await response.AssertErrorAsync(ErrorCode.DiscountExceedsSubtotal);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(3, QuantityOf(order, phoItem));
        Assert.Equal("13.55", order.Money("subtotal"));
        Assert.Equal("9.71", order.Money("discount"));
    }

    [Fact]
    public async Task Fixed_discount_equal_to_the_new_subtotal_allows_the_change()
    {
        var (orderId, phoItem, _) = await OrderWithPhoAndTeaAsync(3.85m, 3, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { amount = 9.70m })).AssertSuccessAsync();

        var response = await _client.ChangeItemQuantityAsync(orderId, phoItem, 2);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("9.70", order.Money("subtotal"));
        Assert.Equal("9.70", order.Money("discount"));
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task The_current_quantity_succeeds_and_leaves_the_order_unchanged()
    {
        var (orderId, phoItem, _) = await OrderWithPhoAndTeaAsync(10.00m, 2, 2.00m);
        var before = await _client.GetOrderAsync(orderId);

        var response = await _client.ChangeItemQuantityAsync(orderId, phoItem, 2);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal(2, QuantityOf(order, phoItem));
        Assert.Equal(before.Money("total"), order.Money("total"));
        Assert.Equal("23.95", order.Money("total")); // 22.00 + tax 1.9525
    }

    [Fact]
    public async Task The_largest_quantity_is_accepted()
    {
        var (orderId, _, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeItemQuantityAsync(orderId, teaItem, int.MaxValue);

        await response.AssertSuccessAsync();
        Assert.Equal(int.MaxValue, QuantityOf(await _client.GetOrderAsync(orderId), teaItem));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Quantity_below_one_is_invalid_and_leaves_the_item_unchanged(int quantity)
    {
        var (orderId, _, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeItemQuantityAsync(orderId, teaItem, quantity);

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
        Assert.Equal(1, QuantityOf(await _client.GetOrderAsync(orderId), teaItem));
    }

    [Fact]
    public async Task Missing_quantity_is_invalid()
    {
        var (orderId, _, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.PutAsJsonAsync($"/api/orders/{orderId}/items/{teaItem}/quantity", new { });

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Empty_item_id_is_not_found()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeItemQuantityAsync(orderId, Guid.Empty, 2);

        await response.AssertErrorAsync(ErrorCode.OrderItemNotFound);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var response = await _client.ChangeItemQuantityAsync(Guid.NewGuid(), Guid.NewGuid(), 2);

        await response.AssertErrorAsync(ErrorCode.OrderNotFound);
    }

    [Fact]
    public async Task Unknown_or_removed_item_is_not_found()
    {
        var (orderId, _, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        await (await _client.RemoveItemAsync(orderId, teaItem)).AssertSuccessAsync();

        await (await _client.ChangeItemQuantityAsync(orderId, teaItem, 2)).AssertErrorAsync(ErrorCode.OrderItemNotFound);
        await (await _client.ChangeItemQuantityAsync(orderId, Guid.NewGuid(), 2)).AssertErrorAsync(ErrorCode.OrderItemNotFound);
    }

    [Fact]
    public async Task Item_of_another_order_is_not_found_and_leaves_both_orders_unchanged()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        var (otherOrderId, _, otherTeaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeItemQuantityAsync(orderId, otherTeaItem, 5);

        await response.AssertErrorAsync(ErrorCode.OrderItemNotFound);
        Assert.Equal("12.00", (await _client.GetOrderAsync(orderId)).Money("subtotal"));
        Assert.Equal(1, QuantityOf(await _client.GetOrderAsync(otherOrderId), otherTeaItem));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Changing_a_quantity_on_a_closed_order_is_rejected_and_leaves_it_unchanged(string status)
    {
        var (orderId, _, teaItem) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        await _client.CloseOrderAsync(orderId, status);

        var response = await _client.ChangeItemQuantityAsync(orderId, teaItem, 3);

        await response.AssertErrorAsync(ErrorCode.OrderNotOpen);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(1, QuantityOf(order, teaItem));
        Assert.Equal("13.07", order.Money("total")); // 12.00 + tax 1.065
    }

    private static int QuantityOf(JsonElement order, Guid itemId) => order.GetProperty("items").EnumerateArray()
        .Single(l => l.GetProperty("id").GetGuid() == itemId).GetProperty("quantity").GetInt32();

    private async Task<(Guid OrderId, Guid PhoItemId, Guid TeaItemId)> OrderWithPhoAndTeaAsync(
        decimal pho, int phoQuantity, decimal tea)
    {
        var phoItem = await _client.CreateMenuItemAsync("Pho", pho);
        var teaItem = await _client.CreateMenuItemAsync("Tea", tea);
        var orderId = await _client.CreateOrderAsync();
        await (await _client.AddItemAsync(orderId, phoItem, phoQuantity)).AssertSuccessAsync();
        var response = await _client.AddItemAsync(orderId, teaItem, 1);
        await response.AssertSuccessAsync();
        var items = (await response.ReadDataAsync()).GetProperty("items").EnumerateArray().ToList();
        Guid ItemOf(Guid menuItemId) =>
            items.Single(l => l.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid();
        return (orderId, ItemOf(phoItem), ItemOf(teaItem));
    }

    private async Task AssertListTotalEqualsGetTotalAsync(Guid orderId)
    {
        var order = await _client.GetOrderAsync(orderId);
        var number = order.GetProperty("number").GetInt32();
        var list = await (await _client.GetAsync($"/api/orders?search={number}&pageSize=100")).ReadDataAsync();
        var listed = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(order.Money("total"), listed.Money("total"));
    }

}
