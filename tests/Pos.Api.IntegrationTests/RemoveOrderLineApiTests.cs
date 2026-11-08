using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>DELETE /api/orders/{id}/lines/{lineId}. Pricing config: tax 8.875%, card surcharge 3%.</summary>
public class RemoveOrderLineApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Removing_a_line_returns_the_repriced_order()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);

        var response = await _client.RemoveLineAsync(orderId, teaLine);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("Pho", Assert.Single(order.GetProperty("lines").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("10.00", order.Money("subtotal"));
        Assert.Equal("0.89", order.Money("tax")); // 8.875% of 10.00 = 0.8875
        Assert.Equal("10.89", order.Money("total"));
        Assert.Equal("10.89", (await _client.GetOrderAsync(orderId)).Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Percent_discount_follows_the_new_subtotal_and_midpoint_rounds_away_from_zero()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(0.70m, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).AssertSuccessAsync();

        var order = await (await _client.RemoveLineAsync(orderId, teaLine)).ReadDataAsync();

        Assert.Equal("0.70", order.Money("subtotal"));
        Assert.Equal("0.11", order.Money("discount")); // 15% of 0.70 = 0.105
        Assert.Equal("0.05", order.Money("tax")); // 8.875% of 0.59 = 0.0523625
        Assert.Equal("0.64", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Fixed_discount_more_than_the_new_subtotal_is_rejected_and_leaves_the_order_unchanged()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(7.70m, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { amount = 7.71m })).AssertSuccessAsync();

        var response = await _client.RemoveLineAsync(orderId, teaLine);

        await response.AssertErrorAsync(ErrorCode.DiscountExceedsSubtotal);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(2, order.GetProperty("lines").GetArrayLength());
        Assert.Equal("7.71", order.Money("discount"));
    }

    [Fact]
    public async Task Fixed_discount_equal_to_the_new_subtotal_allows_the_removal()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(7.70m, 2.00m);
        await (await _client.ApplyDiscountAsync(orderId, new { amount = 7.70m })).AssertSuccessAsync();

        var response = await _client.RemoveLineAsync(orderId, teaLine);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("7.70", order.Money("discount"));
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task Removing_the_last_line_leaves_the_order_open_and_empty()
    {
        var (orderId, phoLine, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);
        await (await _client.RemoveLineAsync(orderId, teaLine)).AssertSuccessAsync();

        var response = await _client.RemoveLineAsync(orderId, phoLine);

        await response.AssertSuccessAsync();
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(0, order.GetProperty("lines").GetArrayLength());
        Assert.Equal("Open", order.GetProperty("status").GetString());
        Assert.Equal("0.00", order.Money("subtotal"));
        Assert.Equal("0.00", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Removing_from_an_unknown_order_is_not_found()
    {
        var response = await _client.RemoveLineAsync(Guid.NewGuid(), Guid.NewGuid());

        await response.AssertErrorAsync(ErrorCode.OrderNotFound);
    }

    [Fact]
    public async Task Removing_an_unknown_or_already_removed_line_is_not_found()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);
        await (await _client.RemoveLineAsync(orderId, teaLine)).AssertSuccessAsync();

        await (await _client.RemoveLineAsync(orderId, teaLine)).AssertErrorAsync(ErrorCode.OrderLineNotFound);
        await (await _client.RemoveLineAsync(orderId, Guid.NewGuid())).AssertErrorAsync(ErrorCode.OrderLineNotFound);
    }

    [Fact]
    public async Task Removing_a_line_of_another_order_is_not_found_and_leaves_both_orders_unchanged()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);
        var (otherOrderId, _, otherTeaLine) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);

        var response = await _client.RemoveLineAsync(orderId, otherTeaLine);

        await response.AssertErrorAsync(ErrorCode.OrderLineNotFound);
        Assert.Equal(2, (await _client.GetOrderAsync(orderId)).GetProperty("lines").GetArrayLength());
        Assert.Equal(2, (await _client.GetOrderAsync(otherOrderId)).GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task Empty_line_id_is_not_found()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);

        var response = await _client.RemoveLineAsync(orderId, Guid.Empty);

        await response.AssertErrorAsync(ErrorCode.OrderLineNotFound);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Removing_a_line_of_a_closed_order_is_rejected_and_leaves_it_unchanged(string status)
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 2.00m);
        await _client.CloseOrderAsync(orderId, status);

        var response = await _client.RemoveLineAsync(orderId, teaLine);

        await response.AssertErrorAsync(ErrorCode.OrderNotOpen);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(2, order.GetProperty("lines").GetArrayLength());
        Assert.Equal("13.07", order.Money("total")); // 12.00 + tax 1.065
    }

    private async Task<(Guid OrderId, Guid PhoLineId, Guid TeaLineId)> OrderWithPhoAndTeaAsync(decimal pho, decimal tea)
    {
        var phoItem = await _client.CreateMenuItemAsync("Pho", pho);
        var teaItem = await _client.CreateMenuItemAsync("Tea", tea);
        var orderId = await _client.CreateOrderAsync();
        await (await _client.AddItemAsync(orderId, phoItem, 1)).AssertSuccessAsync();
        var response = await _client.AddItemAsync(orderId, teaItem, 1);
        await response.AssertSuccessAsync();
        var lines = (await response.ReadDataAsync()).GetProperty("lines").EnumerateArray().ToList();
        Guid LineOf(Guid menuItemId) =>
            lines.Single(l => l.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid();
        return (orderId, LineOf(phoItem), LineOf(teaItem));
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
