using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Pos.Application.MenuItems;
using Pos.Application.Orders;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// GET /api/orders against a real PostgreSQL database. Tests in this class share one
/// database, so each test creates menu items with a unique name and searches for it.
/// </summary>
public class OrdersListApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_returns_summaries_with_item_count_and_total()
    {
        var tag = NewTag();
        var item = await CreateMenuItemAsync($"Mocha {tag}", 3.20m);
        var order = await CreateOrderAsync();
        await AddItemAsync(order, item, 3);

        var rows = await ListAsync($"?search={tag}");

        var row = Assert.Single(rows);
        Assert.Equal(order, row.Id);
        Assert.Equal("Open", row.Status);
        Assert.Equal(3, row.ItemCount);
        Assert.Equal(9.60m, row.Total);
    }

    [Fact]
    public async Task List_without_filters_includes_empty_orders_with_zero_total()
    {
        var order = await CreateOrderAsync();

        var rows = await ListAsync("?limit=200");

        var row = Assert.Single(rows, r => r.Id == order);
        Assert.Equal(0, row.ItemCount);
        Assert.Equal(0m, row.Total);
    }

    [Fact]
    public async Task Search_matches_item_name_case_insensitively_and_partially()
    {
        var tag = NewTag();
        var item = await CreateMenuItemAsync($"Hazelnut Latte {tag}", 4.00m);
        var matching = await CreateOrderAsync();
        await AddItemAsync(matching, item, 1);
        var other = await CreateOrderAsync();

        var rows = await ListAsync($"?search=HAZELNUT%20latte%20{tag.ToUpperInvariant()}");

        Assert.Equal([matching], rows.Select(r => r.Id));
        Assert.DoesNotContain(rows, r => r.Id == other);
    }

    [Fact]
    public async Task Search_matches_order_id_prefix()
    {
        var order = await CreateOrderAsync();

        var rows = await ListAsync($"?search={order.ToString()[..13]}");

        Assert.Contains(rows, r => r.Id == order);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_literally()
    {
        var tag = NewTag();
        var item = await CreateMenuItemAsync($"Tea {tag}", 2.00m);
        var order = await CreateOrderAsync();
        await AddItemAsync(order, item, 1);

        // "%" and "_" would match everything if they were not escaped.
        Assert.Empty(await ListAsync("?search=%25"));
        Assert.Empty(await ListAsync("?search=_"));
    }

    [Fact]
    public async Task Status_filter_returns_only_orders_with_that_status()
    {
        var tag = NewTag();
        var item = await CreateMenuItemAsync($"Scone {tag}", 2.80m);
        var open = await CreateOrderAsync();
        var paid = await CreateOrderAsync();
        await AddItemAsync(open, item, 1);
        await AddItemAsync(paid, item, 2);
        await SetStatusAsync(paid, "Paid");

        var paidRows = await ListAsync($"?status=paid&search={tag}");
        var openRows = await ListAsync($"?status=Open&search={tag}");
        var allRows = await ListAsync($"?search={tag}");

        var paidRow = Assert.Single(paidRows);
        Assert.Equal(paid, paidRow.Id);
        Assert.Equal("Paid", paidRow.Status);
        Assert.Equal(5.60m, paidRow.Total);
        Assert.Equal([open], openRows.Select(r => r.Id));
        Assert.Equal(2, allRows.Count);
    }

    [Fact]
    public async Task List_is_sorted_newest_first_and_respects_limit()
    {
        var tag = NewTag();
        var item = await CreateMenuItemAsync($"Bagel {tag}", 1.50m);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var id = await CreateOrderAsync();
            await AddItemAsync(id, item, 1);
            await SetCreatedAtAsync(id, new DateTime(2026, 1, 1 + i, 8, 0, 0, DateTimeKind.Utc));
            ids.Add(id);
        }

        var all = await ListAsync($"?search={tag}");
        var limited = await ListAsync($"?search={tag}&limit=2");

        Assert.Equal([ids[2], ids[1], ids[0]], all.Select(r => r.Id));
        Assert.Equal(new DateTime(2026, 1, 3, 8, 0, 0, DateTimeKind.Utc), all[0].CreatedAtUtc);
        Assert.Equal([ids[2], ids[1]], limited.Select(r => r.Id));
    }

    [Theory]
    [InlineData("?status=Shipped")]
    [InlineData("?status=1")]
    [InlineData("?limit=0")]
    [InlineData("?limit=201")]
    public async Task Invalid_query_returns_400_problem(string query)
    {
        var response = await _client.GetAsync($"/api/orders{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(400, problem!.Status);
    }

    private static string NewTag() => Guid.NewGuid().ToString("N")[..10];

    private async Task<List<OrderSummaryDto>> ListAsync(string query)
    {
        var response = await _client.GetAsync($"/api/orders{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<OrderSummaryDto>>())!;
    }

    private async Task<Guid> CreateMenuItemAsync(string name, decimal price)
    {
        var response = await _client.PostAsJsonAsync("/api/menu-items", new CreateMenuItemRequest(name, price));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MenuItemDto>())!.Id;
    }

    private async Task<Guid> CreateOrderAsync()
    {
        var response = await _client.PostAsync("/api/orders", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!.Id;
    }

    private async Task AddItemAsync(Guid orderId, Guid menuItemId, int quantity) =>
        (await _client.PostAsJsonAsync($"/api/orders/{orderId}/items", new AddOrderItemRequest(menuItemId, quantity)))
            .EnsureSuccessStatusCode();

    // There is no API to pay/cancel an order or backdate it yet, so tests set these directly.
    private Task SetStatusAsync(Guid orderId, string status) =>
        ExecuteAsync("UPDATE orders SET status = @value WHERE id = @id", orderId, status);

    private Task SetCreatedAtAsync(Guid orderId, DateTime createdAtUtc) =>
        ExecuteAsync("UPDATE orders SET created_at = @value WHERE id = @id", orderId, createdAtUtc);

    private async Task ExecuteAsync(string sql, Guid orderId, object value)
    {
        await using var conn = new NpgsqlConnection(factory.TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", orderId);
        cmd.Parameters.AddWithValue("value", value);
        Assert.Equal(1, await cmd.ExecuteNonQueryAsync());
    }
}
