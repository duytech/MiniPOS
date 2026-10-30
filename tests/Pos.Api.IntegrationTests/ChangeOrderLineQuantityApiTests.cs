using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pos.Api.IntegrationTests;

/// <summary>PUT /api/orders/{id}/lines/{lineId}/quantity. Pricing config: tax 8.875%, card surcharge 3%.</summary>
public class ChangeOrderLineQuantityApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Changing_a_quantity_returns_the_repriced_order()
    {
        var (orderId, phoLine, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeLineQuantityAsync(orderId, teaLine, 3);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.Equal(3, QuantityOf(order, teaLine));
        Assert.Equal(1, QuantityOf(order, phoLine));
        Assert.Equal("16.00", order.Money("subtotal"));
        Assert.Equal("1.42", order.Money("tax")); // 8.875% of 16.00 = 1.42
        Assert.Equal("17.43", order.Money("total"));
        Assert.Equal("17.42", (await _client.GetOrderAsync(orderId)).Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Percent_discount_follows_the_new_subtotal_and_midpoint_rounds_away_from_zero()
    {
        var (orderId, phoLine, _) = await OrderWithPhoAndTeaAsync(0.35m, 1, 2.00m);
        (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).EnsureSuccessStatusCode();

        var order = await (await _client.ChangeLineQuantityAsync(orderId, phoLine, 2)).ReadJsonAsync();

        Assert.Equal("2.70", order.Money("subtotal"));
        Assert.Equal("0.41", order.Money("discount")); // 15% of 2.70 = 0.405
        Assert.Equal("0.20", order.Money("tax")); // 8.875% of 2.29 = 0.2032375
        Assert.Equal("2.49", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Fixed_discount_more_than_the_new_subtotal_is_422_and_leaves_the_order_unchanged()
    {
        var (orderId, phoLine, _) = await OrderWithPhoAndTeaAsync(3.85m, 3, 2.00m);
        (await _client.ApplyDiscountAsync(orderId, new { amount = 9.71m })).EnsureSuccessStatusCode();

        var response = await _client.ChangeLineQuantityAsync(orderId, phoLine, 2); // subtotal would be 9.70

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(3, QuantityOf(order, phoLine));
        Assert.Equal("13.55", order.Money("subtotal"));
        Assert.Equal("9.71", order.Money("discount"));
    }

    [Fact]
    public async Task Fixed_discount_equal_to_the_new_subtotal_allows_the_change()
    {
        var (orderId, phoLine, _) = await OrderWithPhoAndTeaAsync(3.85m, 3, 2.00m);
        (await _client.ApplyDiscountAsync(orderId, new { amount = 9.70m })).EnsureSuccessStatusCode();

        var response = await _client.ChangeLineQuantityAsync(orderId, phoLine, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.Equal("9.70", order.Money("subtotal"));
        Assert.Equal("9.70", order.Money("discount"));
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task The_current_quantity_is_200_and_leaves_the_order_unchanged()
    {
        var (orderId, phoLine, _) = await OrderWithPhoAndTeaAsync(10.00m, 2, 2.00m);
        var before = await _client.GetOrderAsync(orderId);

        var response = await _client.ChangeLineQuantityAsync(orderId, phoLine, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.Equal(2, QuantityOf(order, phoLine));
        Assert.Equal(before.Money("total"), order.Money("total"));
        Assert.Equal("23.95", order.Money("total")); // 22.00 + tax 1.9525
    }

    [Fact]
    public async Task The_largest_quantity_is_accepted()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeLineQuantityAsync(orderId, teaLine, int.MaxValue);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(int.MaxValue, QuantityOf(await _client.GetOrderAsync(orderId), teaLine));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Quantity_below_one_is_400_and_leaves_the_line_unchanged(int quantity)
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeLineQuantityAsync(orderId, teaLine, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, QuantityOf(await _client.GetOrderAsync(orderId), teaLine));
    }

    [Fact]
    public async Task Missing_quantity_is_400()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.PutAsJsonAsync($"/api/orders/{orderId}/lines/{teaLine}/quantity", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Empty_line_id_is_400()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeLineQuantityAsync(orderId, Guid.Empty, 2);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_is_404()
    {
        var response = await _client.ChangeLineQuantityAsync(Guid.NewGuid(), Guid.NewGuid(), 2);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Unknown_or_removed_line_is_404()
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        (await _client.RemoveLineAsync(orderId, teaLine)).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.ChangeLineQuantityAsync(orderId, teaLine, 2)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.ChangeLineQuantityAsync(orderId, Guid.NewGuid(), 2)).StatusCode);
    }

    [Fact]
    public async Task Line_of_another_order_is_404_and_leaves_both_orders_unchanged()
    {
        var (orderId, _, _) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        var (otherOrderId, _, otherTeaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);

        var response = await _client.ChangeLineQuantityAsync(orderId, otherTeaLine, 5);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("12.00", (await _client.GetOrderAsync(orderId)).Money("subtotal"));
        Assert.Equal(1, QuantityOf(await _client.GetOrderAsync(otherOrderId), otherTeaLine));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Changing_a_quantity_on_a_closed_order_is_422_and_leaves_it_unchanged(string status)
    {
        var (orderId, _, teaLine) = await OrderWithPhoAndTeaAsync(10.00m, 1, 2.00m);
        await _client.CloseOrderAsync(orderId, status);

        var response = await _client.ChangeLineQuantityAsync(orderId, teaLine, 3);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(1, QuantityOf(order, teaLine));
        Assert.Equal("13.07", order.Money("total")); // 12.00 + tax 1.065
    }

    private static int QuantityOf(JsonElement order, Guid lineId) => order.GetProperty("lines").EnumerateArray()
        .Single(l => l.GetProperty("id").GetGuid() == lineId).GetProperty("quantity").GetInt32();

    private async Task<(Guid OrderId, Guid PhoLineId, Guid TeaLineId)> OrderWithPhoAndTeaAsync(
        decimal pho, int phoQuantity, decimal tea)
    {
        var phoItem = await _client.CreateMenuItemAsync("Pho", pho);
        var teaItem = await _client.CreateMenuItemAsync("Tea", tea);
        var orderId = await _client.CreateOrderAsync();
        (await _client.AddItemAsync(orderId, phoItem, phoQuantity)).EnsureSuccessStatusCode();
        var response = await _client.AddItemAsync(orderId, teaItem, 1);
        response.EnsureSuccessStatusCode();
        var lines = (await response.ReadJsonAsync()).GetProperty("lines").EnumerateArray().ToList();
        Guid LineOf(Guid menuItemId) =>
            lines.Single(l => l.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid();
        return (orderId, LineOf(phoItem), LineOf(teaItem));
    }

    private async Task AssertListTotalEqualsGetTotalAsync(Guid orderId)
    {
        var order = await _client.GetOrderAsync(orderId);
        var number = order.GetProperty("number").GetInt32();
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/orders?search={number}&pageSize=100");
        var listed = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(order.Money("total"), listed.Money("total"));
    }

}
