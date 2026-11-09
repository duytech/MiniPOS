using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>Discount, tip and payment method endpoints. Pricing config: tax 8.875%, card surcharge 3%.</summary>
public class OrderAdjustmentsApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Percent_discount_is_taken_off_the_subtotal_before_tax()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.ApplyDiscountAsync(orderId, new { rate = 15 });

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("1.50", order.Money("discount"));
        Assert.Equal("0.75", order.Money("tax")); // 8.875% of 8.50 = 0.754375
        Assert.Equal("9.25", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Percent_discount_midpoint_rounds_away_from_zero()
    {
        var orderId = await OrderWithSubtotalAsync(0.70m);

        var order = await (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).ReadDataAsync();

        Assert.Equal("0.11", order.Money("discount")); // 15% of 0.70 = 0.105
        Assert.Equal("0.05", order.Money("tax")); // 8.875% of 0.59 = 0.0523625
        Assert.Equal("0.64", order.Money("total"));
    }

    [Fact]
    public async Task Fixed_discount_replaces_a_percent_discount()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        await _client.ApplyDiscountAsync(orderId, new { rate = 15 });

        var order = await (await _client.ApplyDiscountAsync(orderId, new { amount = 2.50m })).ReadDataAsync();

        Assert.Equal("2.50", order.Money("discount"));
        Assert.Equal("0.67", order.Money("tax")); // 8.875% of 7.50 = 0.665625
        Assert.Equal("8.17", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Removing_the_discount_restores_the_total()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        await _client.ApplyDiscountAsync(orderId, new { amount = 2.50m });

        var response = await _client.RemoveDiscountAsync(orderId);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("0.00", order.Money("discount"));
        Assert.Equal("10.89", order.Money("total")); // 10.00 + tax 0.8875
        Assert.Equal("10.89", (await _client.GetOrderAsync(orderId)).Money("total"));
    }

    [Fact]
    public async Task Discount_more_than_the_subtotal_is_rejected()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        await (await _client.ApplyDiscountAsync(orderId, new { amount = 10.01m }))
            .AssertErrorAsync(ErrorCode.DiscountExceedsSubtotal);
        await (await _client.ApplyDiscountAsync(orderId, new { rate = 100.01m }))
            .AssertErrorAsync(ErrorCode.ValidationFailed);
        Assert.Equal("0.00", (await _client.GetOrderAsync(orderId)).Money("discount"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "rate": 10, "amount": 1.00 }""")]
    [InlineData("""{ "rate": -1 }""")]
    [InlineData("""{ "rate": 1.00001 }""")]
    [InlineData("""{ "amount": -0.01 }""")]
    [InlineData("""{ "amount": 1.001 }""")]
    [InlineData("""{ "rate": "ten" }""")]
    public async Task Invalid_discount_is_invalid(string body)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.PutAsync($"/api/orders/{orderId}/discount", Json(body));

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Tip_is_on_the_discounted_subtotal_and_midpoint_rounds_away_from_zero()
    {
        var orderId = await OrderWithSubtotalAsync(0.70m);

        var response = await _client.SetTipAsync(orderId, 15m);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("0.11", order.Money("tip")); // 15% of 0.70 = 0.105
        Assert.Equal("0.06", order.Money("tax")); // 8.875% of 0.70 = 0.062125, the tip is not taxed
        Assert.Equal("0.87", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);
    }

    [Fact]
    public async Task Removing_the_tip_restores_the_total()
    {
        var orderId = await OrderWithSubtotalAsync(0.70m);
        await _client.SetTipAsync(orderId, 15m);

        var order = await (await _client.RemoveTipAsync(orderId)).ReadDataAsync();

        Assert.Equal("0.00", order.Money("tip"));
        Assert.Equal("0.76", order.Money("total"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "rate": null }""")]
    [InlineData("""{ "rate": -0.5 }""")]
    [InlineData("""{ "rate": 1000 }""")]
    [InlineData("""{ "rate": 18.12345 }""")]
    public async Task Invalid_tip_is_invalid(string body)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.PutAsync($"/api/orders/{orderId}/tip", Json(body));

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Paying_by_card_adds_the_surcharge_which_is_taxed()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.SetPaymentMethodAsync(orderId, "Card");

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal("Card", order.GetProperty("paymentMethod").GetString());
        Assert.Equal("0.30", order.Money("surcharge"));
        Assert.Equal("0.91", order.Money("tax")); // 8.875% of 10.30 = 0.914125
        Assert.Equal("11.21", order.Money("total"));
        await AssertListTotalEqualsGetTotalAsync(orderId);

        var cash = await (await _client.SetPaymentMethodAsync(orderId, "Cash")).ReadDataAsync();
        Assert.Equal("0.00", cash.Money("surcharge"));
        Assert.Equal("10.89", cash.Money("total"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "paymentMethod": "Bitcoin" }""")]
    [InlineData("""{ "paymentMethod": 42 }""")]
    public async Task Invalid_payment_method_is_invalid(string body)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.PutAsync($"/api/orders/{orderId}/payment-method", Json(body));

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
    }

    public static TheoryData<Func<HttpClient, Guid, Task<HttpResponseMessage>>> Changes => new()
    {
        (c, id) => c.ApplyDiscountAsync(id, new { rate = 10 }),
        (c, id) => c.RemoveDiscountAsync(id),
        (c, id) => c.SetTipAsync(id, 18m),
        (c, id) => c.RemoveTipAsync(id),
        (c, id) => c.SetPaymentMethodAsync(id, "Card"),
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task Changing_an_unknown_order_is_not_found(Func<HttpClient, Guid, Task<HttpResponseMessage>> change)
    {
        var response = await change(_client, Guid.NewGuid());

        await response.AssertErrorAsync(ErrorCode.OrderNotFound);
    }

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task Changing_a_paid_order_is_rejected_and_leaves_it_unchanged(
        Func<HttpClient, Guid, Task<HttpResponseMessage>> change)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        await _client.CloseOrderAsync(orderId, "Paid");

        var response = await change(_client, orderId);

        await response.AssertErrorAsync(ErrorCode.OrderNotOpen);
        Assert.Equal("10.89", (await _client.GetOrderAsync(orderId)).Money("total"));
    }

    [Fact]
    public async Task Adding_an_item_to_a_paid_order_is_rejected_and_leaves_it_unchanged()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        var tea = await _client.CreateMenuItemAsync("Tea", 2.00m);
        await _client.CloseOrderAsync(orderId, "Paid");

        var response = await _client.AddItemAsync(orderId, tea, 1);

        await response.AssertErrorAsync(ErrorCode.OrderNotOpen);
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(1, order.GetProperty("items").GetArrayLength());
        Assert.Equal("10.89", order.Money("total"));
    }

    private async Task<Guid> OrderWithSubtotalAsync(decimal price)
    {
        var menuItem = await _client.CreateMenuItemAsync("Pho", price);
        var orderId = await _client.CreateOrderAsync();
        await (await _client.AddItemAsync(orderId, menuItem, 1)).AssertSuccessAsync();
        return orderId;
    }

    private async Task AssertListTotalEqualsGetTotalAsync(Guid orderId)
    {
        var order = await _client.GetOrderAsync(orderId);
        var number = order.GetProperty("number").GetInt32();
        var list = await (await _client.GetAsync($"/api/orders?search={number}&pageSize=100")).ReadDataAsync();
        var listed = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == orderId);
        Assert.Equal(order.Money("total"), listed.Money("total"));
    }

    private static StringContent Json(string body) => new(body, System.Text.Encoding.UTF8, "application/json");
}
