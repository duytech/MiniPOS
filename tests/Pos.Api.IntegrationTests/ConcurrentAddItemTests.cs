using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Hidden verifier from Day 3, adapted to the Day 6 API (menu items created via the API, table <c>order_lines</c>).
/// Two terminals (or a double-tap) adding to the same order at the same time.
/// The invariant, whatever the fix (retry or 409): no 5xx, every rejected request
/// is a 409, and the stored quantity equals exactly the number of accepted requests.
/// </summary>
public class ConcurrentAddItemTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int ConcurrentRequests = 20;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Concurrent_adds_of_an_existing_line_do_not_lose_quantity()
    {
        var cappuccino = await _client.CreateMenuItemAsync("Cappuccino", 4.50m);
        var orderId = await _client.CreateOrderAsync();
        (await _client.AddItemAsync(orderId, cappuccino, 1)).EnsureSuccessStatusCode();

        var statuses = await AddItemConcurrentlyAsync(orderId, cappuccino);

        AssertNoServerErrorsAndOnlyConflicts(statuses);
        var accepted = statuses.Count(s => s == HttpStatusCode.OK);
        Assert.Equal(1 + accepted, await StoredQuantityAsync(orderId, cappuccino));
    }

    [Fact]
    public async Task Concurrent_first_adds_of_a_new_item_never_return_500()
    {
        var espresso = await _client.CreateMenuItemAsync("Espresso", 3.00m);
        var orderId = await _client.CreateOrderAsync();

        var statuses = await AddItemConcurrentlyAsync(orderId, espresso);

        AssertNoServerErrorsAndOnlyConflicts(statuses);
        var accepted = statuses.Count(s => s == HttpStatusCode.OK);
        Assert.True(accepted >= 1, "At least one of the concurrent adds must succeed.");
        Assert.Equal(accepted, await StoredQuantityAsync(orderId, espresso));
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
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return response.StatusCode;
            })
            .ToList();

        start.SetResult();
        return await Task.WhenAll(requests);
    }

    /// <summary>Reads straight from Postgres so the API's own view can't hide a lost update.</summary>
    private async Task<int> StoredQuantityAsync(Guid orderId, Guid menuItemId)
    {
        string connectionString;
        using (var scope = factory.Services.CreateScope())
        {
            connectionString = scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.GetConnectionString()!;
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(SUM(quantity), 0)::int FROM order_lines WHERE order_id = @o AND menu_item_id = @m", conn);
        cmd.Parameters.AddWithValue("o", orderId);
        cmd.Parameters.AddWithValue("m", menuItemId);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }
}
