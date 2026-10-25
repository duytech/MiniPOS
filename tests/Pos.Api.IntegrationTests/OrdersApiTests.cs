using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Pos.Application.MenuItems;
using Pos.Application.Orders;

namespace Pos.Api.IntegrationTests;

public class OrdersApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    // Seeded by the InitialCreate migration.
    private static readonly Guid Espresso = Guid.Parse("11111111-1111-1111-1111-000000000001");      // 2.50
    private static readonly Guid ClubSandwich = Guid.Parse("11111111-1111-1111-1111-000000000004");  // 7.90

    private readonly HttpClient _client = factory.CreateClient();

    private async Task<OrderDto> CreateOrderAsync()
    {
        var response = await _client.PostAsync("/api/orders", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.EndsWith($"/api/orders/{order.Id}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
        return order;
    }

    private Task<HttpResponseMessage> AddItemAsync(Guid orderId, Guid menuItemId, int quantity) =>
        _client.PostAsJsonAsync($"/api/orders/{orderId}/items", new AddOrderItemRequest(menuItemId, quantity));

    [Fact]
    public async Task Create_order_returns_empty_open_order()
    {
        var order = await CreateOrderAsync();

        Assert.Equal("Open", order.Status);
        Assert.Empty(order.Items);
        Assert.Equal(0m, order.Total);
    }

    [Fact]
    public async Task Add_items_and_calculate_total_is_persisted_in_postgres()
    {
        var order = await CreateOrderAsync();

        (await AddItemAsync(order.Id, Espresso, 2)).EnsureSuccessStatusCode();
        (await AddItemAsync(order.Id, ClubSandwich, 1)).EnsureSuccessStatusCode();
        var response = await AddItemAsync(order.Id, Espresso, 1); // merges into the espresso line
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>();

        Assert.NotNull(updated);
        Assert.Equal(2, updated.Items.Count);
        Assert.Equal(3, updated.Items.Single(i => i.MenuItemId == Espresso).Quantity);
        Assert.Equal(15.40m, updated.Total); // 3 x 2.50 + 1 x 7.90

        // Re-read through a fresh request (new DbContext) to prove it was persisted.
        var fetched = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.NotNull(fetched);
        Assert.Equal(15.40m, fetched.Total);
        Assert.Equal(2, fetched.Items.Count);

        var total = await _client.GetFromJsonAsync<OrderTotalDto>($"/api/orders/{order.Id}/total");
        Assert.NotNull(total);
        Assert.Equal(order.Id, total.OrderId);
        Assert.Equal(4, total.ItemCount);
        Assert.Equal(15.40m, total.Total);

        // And verify directly in the database.
        await using var conn = new NpgsqlConnection(factory.TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(SUM(unit_price * quantity), 0) FROM order_items WHERE order_id = @id", conn);
        cmd.Parameters.AddWithValue("id", order.Id);
        Assert.Equal(15.40m, (decimal)(await cmd.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Menu_item_created_via_api_can_be_added_to_an_order()
    {
        var create = await _client.PostAsJsonAsync("/api/menu-items", new CreateMenuItemRequest("Matcha Latte", 4.25m));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var matcha = await create.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(matcha);

        var order = await CreateOrderAsync();
        var response = await AddItemAsync(order.Id, matcha.Id, 2);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<OrderDto>();

        var line = Assert.Single(updated!.Items);
        Assert.Equal("Matcha Latte", line.Name);
        Assert.Equal(4.25m, line.UnitPrice);
        Assert.Equal(8.50m, updated.Total);
    }

    [Fact]
    public async Task Get_unknown_order_returns_404_problem()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(404, problem!.Status);
    }

    [Fact]
    public async Task Total_of_unknown_order_returns_404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}/total");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Add_unknown_menu_item_returns_404()
    {
        var order = await CreateOrderAsync();

        var response = await AddItemAsync(order.Id, Guid.NewGuid(), 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Contains("Menu item", problem!.Detail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Add_item_with_invalid_quantity_returns_422_and_nothing_is_saved(int quantity)
    {
        var order = await CreateOrderAsync();

        var response = await AddItemAsync(order.Id, Espresso, quantity);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var fetched = await _client.GetFromJsonAsync<OrderDto>($"/api/orders/{order.Id}");
        Assert.Empty(fetched!.Items);
    }

    [Fact]
    public async Task List_menu_items_includes_seeded_items()
    {
        var items = await _client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu-items");

        Assert.NotNull(items);
        Assert.Contains(items, m => m.Id == Espresso && m.Price == 2.50m);
    }
}
