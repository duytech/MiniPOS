using System.Net.Http.Json;
using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

public class OrdersApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_order_returns_zero_totals()
    {
        var response = await _client.PostAsync("/api/orders", null);

        await response.AssertSuccessAsync();
        var order = await response.ReadDataAsync();
        Assert.Equal(0, order.GetProperty("items").GetArrayLength());
        Assert.Equal("0.00", order.Money("subtotal"));
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task Added_items_are_persisted_and_totals_have_exactly_two_decimals()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.7m);
        var tea = await _client.CreateMenuItemAsync("Iced tea", 1.15m);
        var orderId = await _client.CreateOrderAsync();

        await (await _client.AddItemAsync(orderId, pho, 2)).AssertSuccessAsync();
        await (await _client.AddItemAsync(orderId, tea, 3)).AssertSuccessAsync();

        var order = await _client.GetOrderAsync(orderId);
        var items = order.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal("7.70", items[0].Money("unitPrice"));
        Assert.Equal("15.40", items[0].Money("itemTotal"));
        Assert.Equal("1.15", items[1].Money("unitPrice"));
        Assert.Equal("3.45", items[1].Money("itemTotal"));
        Assert.Equal("18.85", order.Money("subtotal"));
        Assert.Equal("0.00", order.Money("discount"));
        Assert.Equal("Cash", order.GetProperty("paymentMethod").GetString());
        Assert.Equal("0.00", order.Money("surcharge"));
        Assert.Equal("1.67", order.Money("tax")); // Pricing:TaxRatePercent 8.875 × 18.85 = 1.6729375
        Assert.Equal("0.00", order.Money("tip"));
        Assert.Equal("20.52", order.Money("total"));
    }

    [Fact]
    public async Task Add_item_response_matches_get_order()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var added = await (await _client.AddItemAsync(orderId, pho, 2)).ReadDataAsync();
        var fetched = await _client.GetOrderAsync(orderId);

        Assert.Equal(fetched.GetRawText(), added.GetRawText());
    }

    [Fact]
    public async Task Adding_an_identical_item_increases_its_quantity()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        await _client.AddItemAsync(orderId, pho, 1, "no onions");
        await _client.AddItemAsync(orderId, pho, 2, "no onions");
        await _client.AddItemAsync(orderId, pho, 1);

        var items = (await _client.GetOrderAsync(orderId)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Equal(3, items.Single(l => l.GetProperty("notes").GetString() == "no onions").GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        await response.AssertErrorAsync(ErrorCode.OrderNotFound);
    }

    [Fact]
    public async Task Adding_to_an_unknown_order_is_not_found()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);

        var response = await _client.AddItemAsync(Guid.NewGuid(), pho, 1);

        await response.AssertErrorAsync(ErrorCode.OrderNotFound);
    }

    [Fact]
    public async Task Adding_an_unknown_menu_item_is_not_found()
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.AddItemAsync(orderId, Guid.NewGuid(), 1);

        await response.AssertErrorAsync(ErrorCode.MenuItemNotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Non_positive_quantity_is_invalid(int quantity)
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.AddItemAsync(orderId, pho, quantity);

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
        Assert.Equal(0, (await _client.GetOrderAsync(orderId)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Quantity_overflow_is_rejected()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();
        await _client.AddItemAsync(orderId, pho, int.MaxValue);

        var response = await _client.AddItemAsync(orderId, pho, 1);

        await response.AssertErrorAsync(ErrorCode.ItemQuantityTooLarge);
    }

    [Theory]
    [InlineData("""{ "name": "Tea", "price": 1.005 }""")]
    [InlineData("""{ "name": "Tea", "price": -1.00 }""")]
    [InlineData("""{ "name": "Tea" }""")]
    [InlineData("""{ "name": "", "price": 1.00 }""")]
    [InlineData("""{ "name": "Tea", "price": "1.00" }""")]
    public async Task Invalid_menu_item_is_invalid(string body)
    {
        var response = await _client.PostAsync(
            "/api/menu-items", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
    }

    [Fact]
    public async Task Menu_item_price_is_returned_with_two_decimals()
    {
        var response = await _client.PostAsJsonAsync("/api/menu-items", new { name = "Tea", price = 2.5m });

        Assert.Equal("2.50", (await response.ReadDataAsync()).Money("price"));
    }
}
