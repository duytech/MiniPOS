using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Pos.Application.Orders;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Two terminals (or a double-tap) adding to the same order at the same time.
/// The invariant, whatever the fix (retry or 409): no 5xx, every rejected request
/// is a 409, and the stored quantity equals exactly the number of accepted requests.
/// </summary>
public class ConcurrentAddItemTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int ConcurrentRequests = 20;

    // Seeded by the InitialCreate migration.
    private static readonly Guid Espresso = Guid.Parse("11111111-1111-1111-1111-000000000001");
    private static readonly Guid Cappuccino = Guid.Parse("11111111-1111-1111-1111-000000000002");

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Concurrent_adds_of_an_existing_line_do_not_lose_quantity()
    {
        var orderId = await CreateOrderAsync();
        (await AddItemAsync(orderId, Cappuccino)).EnsureSuccessStatusCode();

        var statuses = await AddItemConcurrentlyAsync(orderId, Cappuccino);

        AssertNoServerErrorsAndOnlyConflicts(statuses);
        var accepted = statuses.Count(s => s == HttpStatusCode.OK);
        Assert.Equal(1 + accepted, await StoredQuantityAsync(orderId, Cappuccino));
    }

    [Fact]
    public async Task Concurrent_first_adds_of_a_new_item_never_return_500()
    {
        var orderId = await CreateOrderAsync();

        var statuses = await AddItemConcurrentlyAsync(orderId, Espresso);

        AssertNoServerErrorsAndOnlyConflicts(statuses);
        var accepted = statuses.Count(s => s == HttpStatusCode.OK);
        Assert.True(accepted >= 1, "At least one of the concurrent adds must succeed.");
        Assert.Equal(accepted, await StoredQuantityAsync(orderId, Espresso));
    }

    private static void AssertNoServerErrorsAndOnlyConflicts(IReadOnlyList<HttpStatusCode> statuses)
    {
        var unexpected = statuses
            .Where(s => s is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
            .GroupBy(s => s)
            .Select(g => $"{(int)g.Key} x{g.Count()}")
            .ToList();
        Assert.True(unexpected.Count == 0, $"Expected only 200 or 409, got: {string.Join(", ", unexpected)}");
    }

    /// <summary>Releases all requests at once so they hit the same order row together.</summary>
    private async Task<IReadOnlyList<HttpStatusCode>> AddItemConcurrentlyAsync(Guid orderId, Guid menuItemId)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = Enumerable.Range(0, ConcurrentRequests)
            .Select(async _ =>
            {
                await start.Task;
                using var response = await AddItemAsync(orderId, menuItemId);
                return response.StatusCode;
            })
            .ToList();

        start.SetResult();
        return await Task.WhenAll(requests);
    }

    private async Task<Guid> CreateOrderAsync()
    {
        var response = await _client.PostAsync("/api/orders", null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>())!.Id;
    }

    private Task<HttpResponseMessage> AddItemAsync(Guid orderId, Guid menuItemId) =>
        _client.PostAsJsonAsync($"/api/orders/{orderId}/items", new AddOrderItemRequest(menuItemId, 1));

    /// <summary>Reads straight from Postgres so the API's own view can't hide a lost update.</summary>
    private async Task<int> StoredQuantityAsync(Guid orderId, Guid menuItemId)
    {
        await using var conn = new NpgsqlConnection(factory.TestConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(SUM(quantity), 0)::int FROM order_items WHERE order_id = @o AND menu_item_id = @m", conn);
        cmd.Parameters.AddWithValue("o", orderId);
        cmd.Parameters.AddWithValue("m", menuItemId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
}
