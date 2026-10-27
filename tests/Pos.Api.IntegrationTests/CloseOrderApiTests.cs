using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pos.Api.IntegrationTests;

/// <summary>Pay and cancel endpoints. Pricing config: tax 8.875%, card surcharge 3%.</summary>
public class CloseOrderApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("pay", "Paid")]
    [InlineData("cancel", "Cancelled")]
    public async Task Closing_an_open_order_sets_the_status_and_its_time(string action, string status)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        var before = await _client.GetOrderAsync(orderId);

        var response = await CloseAsync(action, orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.Equal(status, order.GetProperty("status").GetString());
        var (closedAt, otherAt) = action == "pay" ? ("paidAt", "cancelledAt") : ("cancelledAt", "paidAt");
        Assert.True(order.GetProperty(closedAt).GetDateTimeOffset()
                    >= before.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, order.GetProperty(otherAt).ValueKind);
        Assert.Equal("10.89", order.Money("total")); // 10.00 + tax 0.8875

        var stored = await _client.GetOrderAsync(orderId);
        Assert.Equal(status, stored.GetProperty("status").GetString());
        // Postgres keeps microseconds; the response of the pay/cancel request still has the clock's 100 ns ticks.
        Assert.Equal(order.GetProperty(closedAt).GetDateTimeOffset(), stored.GetProperty(closedAt).GetDateTimeOffset(),
            TimeSpan.FromMicroseconds(1));
        Assert.Equal(JsonValueKind.Null, stored.GetProperty(otherAt).ValueKind);
        Assert.Equal("10.89", stored.Money("total"));
    }

    [Fact]
    public async Task A_new_order_is_neither_paid_nor_cancelled()
    {
        var order = await _client.GetOrderAsync(await _client.CreateOrderAsync());

        Assert.Equal(JsonValueKind.Null, order.GetProperty("paidAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, order.GetProperty("cancelledAt").ValueKind);
    }

    [Theory]
    [InlineData("pay", "Paid")]
    [InlineData("cancel", "Cancelled")]
    public async Task An_empty_order_can_be_closed(string action, string status)
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await CloseAsync(action, orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.Equal(status, order.GetProperty("status").GetString());
        Assert.Equal(0, order.GetProperty("lines").GetArrayLength());
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task Paying_keeps_the_totals_and_midpoint_rounds_away_from_zero()
    {
        var orderId = await OrderWithSubtotalAsync(0.70m);
        (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).EnsureSuccessStatusCode();

        var order = await (await _client.PayOrderAsync(orderId)).ReadJsonAsync();

        Assert.Equal("Paid", order.GetProperty("status").GetString());
        Assert.Equal("0.70", order.Money("subtotal"));
        Assert.Equal("0.11", order.Money("discount")); // 15% of 0.70 = 0.105
        Assert.Equal("0.05", order.Money("tax")); // 8.875% of 0.59 = 0.0523625
        Assert.Equal("0.64", order.Money("total"));
        Assert.Equal("0.64", (await _client.GetOrderAsync(orderId)).Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Paying_by_card_returns_the_same_money_as_before_paying()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        (await _client.SetPaymentMethodAsync(orderId, "Card")).EnsureSuccessStatusCode();
        (await _client.SetTipAsync(orderId, 18m)).EnsureSuccessStatusCode();
        var before = await _client.GetOrderAsync(orderId);

        var paid = await (await _client.PayOrderAsync(orderId)).ReadJsonAsync();

        Assert.Equal("Card", paid.GetProperty("paymentMethod").GetString());
        foreach (var amount in new[] { "subtotal", "discount", "surcharge", "tax", "tip", "total" })
        {
            Assert.Equal(before.Money(amount), paid.Money(amount));
        }

        Assert.Equal("0.30", paid.Money("surcharge"));
        Assert.Equal("13.01", paid.Money("total")); // 10.00 + surcharge 0.30 + tax 0.914125 + tip 1.80
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Theory]
    [InlineData("pay")]
    [InlineData("cancel")]
    public async Task Empty_order_id_is_400(string action)
    {
        var response = await CloseAsync(action, Guid.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("pay")]
    [InlineData("cancel")]
    public async Task Closing_an_unknown_order_is_404(string action)
    {
        var response = await CloseAsync(action, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("pay", "pay")]
    [InlineData("pay", "cancel")]
    [InlineData("cancel", "cancel")]
    [InlineData("cancel", "pay")]
    public async Task Closing_a_closed_order_is_422_and_leaves_it_unchanged(string first, string second)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        (await CloseAsync(first, orderId)).EnsureSuccessStatusCode();
        var before = await _client.GetOrderAsync(orderId);

        var response = await CloseAsync(second, orderId);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var after = await _client.GetOrderAsync(orderId);
        Assert.Equal(before.GetProperty("status").GetString(), after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("paidAt").GetRawText(), after.GetProperty("paidAt").GetRawText());
        Assert.Equal(before.GetProperty("cancelledAt").GetRawText(), after.GetProperty("cancelledAt").GetRawText());
        Assert.Equal("10.89", after.Money("total"));
    }

    private Task<HttpResponseMessage> CloseAsync(string action, Guid orderId) => action == "pay"
        ? _client.PayOrderAsync(orderId)
        : _client.CancelOrderAsync(orderId);

    private async Task<Guid> OrderWithSubtotalAsync(decimal price)
    {
        var menuItem = await _client.CreateMenuItemAsync("Pho", price);
        var orderId = await _client.CreateOrderAsync();
        (await _client.AddItemAsync(orderId, menuItem, 1)).EnsureSuccessStatusCode();
        return orderId;
    }

    private async Task AssertListTotalEqualsGetTotalAsync(Guid orderId)
    {
        var order = await _client.GetOrderAsync(orderId);
        var number = order.GetProperty("number").GetInt32();
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/orders?search={number}&status=Paid&pageSize=100");
        var listed = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(order.Money("total"), listed.Money("total"));
    }
}
