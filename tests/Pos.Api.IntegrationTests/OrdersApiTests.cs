using System.Net;
using System.Net.Http.Json;

namespace Pos.Api.IntegrationTests;

public class OrdersApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_order_returns_201_with_location_and_zero_totals()
    {
        var response = await _client.PostAsync("/api/orders", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.ReadJsonAsync();
        Assert.EndsWith($"/api/orders/{order.GetProperty("id").GetGuid()}", response.Headers.Location!.ToString());
        Assert.Equal(0, order.GetProperty("lines").GetArrayLength());
        Assert.Equal("0.00", order.Money("subtotal"));
        Assert.Equal("0.00", order.Money("total"));
    }

    [Fact]
    public async Task Added_items_are_persisted_and_totals_have_exactly_two_decimals()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.7m);
        var tea = await _client.CreateMenuItemAsync("Iced tea", 1.15m);
        var orderId = await _client.CreateOrderAsync();

        Assert.Equal(HttpStatusCode.OK, (await _client.AddItemAsync(orderId, pho, 2)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.AddItemAsync(orderId, tea, 3)).StatusCode);

        var order = await _client.GetOrderAsync(orderId);
        var lines = order.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal("7.70", lines[0].Money("unitPrice"));
        Assert.Equal("15.40", lines[0].Money("lineTotal"));
        Assert.Equal("1.15", lines[1].Money("unitPrice"));
        Assert.Equal("3.45", lines[1].Money("lineTotal"));
        Assert.Equal("18.85", order.Money("subtotal"));
        Assert.Equal("18.85", order.Money("total"));
    }

    [Fact]
    public async Task Add_item_response_matches_get_order()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var added = await (await _client.AddItemAsync(orderId, pho, 2)).ReadJsonAsync();
        var fetched = await _client.GetOrderAsync(orderId);

        Assert.Equal(fetched.GetRawText(), added.GetRawText());
    }

    [Fact]
    public async Task Adding_an_identical_line_increases_its_quantity()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        await _client.AddItemAsync(orderId, pho, 1, "no onions");
        await _client.AddItemAsync(orderId, pho, 2, "no onions");
        await _client.AddItemAsync(orderId, pho, 1);

        var lines = (await _client.GetOrderAsync(orderId)).GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(3, lines.Single(l => l.GetProperty("notes").GetString() == "no onions").GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task Unknown_order_is_404_problem_details()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Adding_to_an_unknown_order_is_404()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);

        var response = await _client.AddItemAsync(Guid.NewGuid(), pho, 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_an_unknown_menu_item_is_404()
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.AddItemAsync(orderId, Guid.NewGuid(), 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Non_positive_quantity_is_400(int quantity)
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.AddItemAsync(orderId, pho, quantity);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, (await _client.GetOrderAsync(orderId)).GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task Quantity_overflow_is_422()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();
        await _client.AddItemAsync(orderId, pho, int.MaxValue);

        var response = await _client.AddItemAsync(orderId, pho, 1);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "Tea", "price": 1.005 }""")]
    [InlineData("""{ "name": "Tea", "price": -1.00 }""")]
    [InlineData("""{ "name": "Tea" }""")]
    [InlineData("""{ "name": "", "price": 1.00 }""")]
    [InlineData("""{ "name": "Tea", "price": "1.00" }""")]
    public async Task Invalid_menu_item_is_400(string body)
    {
        var response = await _client.PostAsync(
            "/api/menu-items", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Menu_item_price_is_returned_with_two_decimals()
    {
        var response = await _client.PostAsJsonAsync("/api/menu-items", new { name = "Tea", price = 2.5m });

        Assert.Equal("2.50", (await response.ReadJsonAsync()).Money("price"));
    }
}
