using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pos.Domain.Common;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Saving and removing a split go through the order's concurrency token (the orders row is touched), so they race
/// each other and add-item like any order change. The invariant: no 5xx, and the stored split rows are exactly one
/// accepted request's split, or none, and never name items the order does not have or miss one it has.
/// </summary>
public class ConcurrentSplitTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int ConcurrentRequests = 8;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Parallel_puts_store_exactly_one_accepted_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(4);
        int[][][] groupings =
        [
            [[0], [1, 2, 3]], [[1], [0, 2, 3]], [[2], [0, 1, 3]], [[3], [0, 1, 2]],
            [[0, 1], [2, 3]], [[0, 2], [1, 3]], [[0, 3], [1, 2]], [[0], [1], [2], [3]],
        ];

        var results = await OrderRace.RunAsync(factory.ConnectionString, orderId, groupings.Select(grouping =>
            (Func<Task<(ErrorCode?, string)>>)(async () =>
        {
            var parts = grouping.Select(part => part.Select(i => l[i])).ToArray();
            using var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody(parts));
            return (await response.OutcomeAsync(), Describe(parts));
        })));

        var accepted = AssertOnly(results, null, ErrorCode.ConcurrentChange, ErrorCode.DataConflict);
        Assert.Single(accepted);
        Assert.Contains(await StoredSplitAsync(orderId), accepted);
    }

    /// <summary>
    /// Every PUT names the first four items only. All requests load the order before any commits
    /// (<see cref="OrderRace"/>), so a PUT whose split an add would make stale must lose on the token. Whatever
    /// wins, no add is lost and a stored split covers exactly the stored items. A PUT that loads the order after an add
    /// committed (an item left out) is covered by the split contract tests.
    /// </summary>
    [Fact]
    public async Task Puts_racing_add_items_lose_no_item_and_never_store_a_stale_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(4);
        var newMenuItemIds = new List<Guid>();
        for (var i = 0; i < ConcurrentRequests; i++)
        {
            newMenuItemIds.Add(await _client.CreateMenuItemAsync($"Extra {i}", 2.00m));
        }

        object[] bodies = [ApiClient.SplitBody([l[0], l[1]], [l[2], l[3]]), ApiClient.SplitBody([l[0]], [l[1], l[2], l[3]])];

        var requests = newMenuItemIds.SelectMany((menuItemId, i) => new Func<Task<(ErrorCode?, string)>>[]
        {
            async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (await response.OutcomeAsync(), "add");
            },
            async () =>
            {
                using var response = await _client.SaveSplitAsync(orderId, bodies[i % 2]);
                return (await response.OutcomeAsync(), "put");
            },
        });
        var results = await OrderRace.RunAsync(factory.ConnectionString, orderId, requests);

        Assert.Single(AssertOnly(results, null, ErrorCode.ConcurrentChange, ErrorCode.DataConflict));
        var acceptedAdds = results.Count(r => r is (null, "add"));
        Assert.Equal(4 + acceptedAdds, await StoredAsync<int>(
            orderId, "(SELECT COUNT(*)::int FROM order_menu_items WHERE order_id = o.id)"));
        var storedSplitItems = await StoredAsync<string>(orderId,
            "(SELECT COALESCE(string_agg(spl.order_menu_item_id::text, ',' ORDER BY spl.order_menu_item_id::text COLLATE \"C\"), '') " +
            "FROM order_split_parts sp JOIN order_split_part_menu_items spl ON spl.part_id = sp.id WHERE sp.order_id = o.id)");
        if (storedSplitItems.Length > 0)
        {
            Assert.Equal(await StoredAsync<string>(orderId,
                "(SELECT string_agg(id::text, ',' ORDER BY id::text COLLATE \"C\") FROM order_menu_items WHERE order_id = o.id)"),
                storedSplitItems);
        }
    }

    /// <summary>
    /// The order starts with a saved split; DELETEs race PUTs of a different split. All of them load the order before
    /// any commits (<see cref="OrderRace"/>), so exactly one is accepted and the others conflict. The stored split is the
    /// PUT's or none, matching who was accepted.
    /// </summary>
    [Fact]
    public async Task Parallel_deletes_and_puts_leave_a_consistent_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(3);
        await (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1], l[2]]))).AssertSuccessAsync();
        IEnumerable<Guid>[] putParts = [[l[2]], [l[0], l[1]]];

        var requests = Enumerable.Range(0, ConcurrentRequests / 2).SelectMany(_ => new Func<Task<(ErrorCode?, string)>>[]
        {
            async () =>
            {
                using var response = await _client.RemoveSplitAsync(orderId);
                return (await response.OutcomeAsync(), "delete");
            },
            async () =>
            {
                using var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody(putParts));
                return (await response.OutcomeAsync(), "put");
            },
        });
        var results = await OrderRace.RunAsync(factory.ConnectionString, orderId, requests);

        Assert.Single(AssertOnly(results, null, ErrorCode.ConcurrentChange, ErrorCode.DataConflict));
        var stored = await StoredSplitAsync(orderId);
        if (stored.Length == 0)
        {
            Assert.Contains(results, r => r is (null, "delete"));
        }
        else
        {
            Assert.Equal(Describe(putParts), stored);
            Assert.Contains(results, r => r is (null, "put"));
        }
    }

    /// <summary>Parts in order, separated by '|'; the item ids of a part sorted, to match the sorted SQL read of the stored rows (item order is checked in SplitBillContractTests).</summary>
    private static string Describe(IEnumerable<IEnumerable<Guid>> parts) =>
        string.Join("|", parts.Select(part => string.Join(",", part.Select(id => id.ToString()).Order(StringComparer.Ordinal))));

    /// <summary>The stored split in the same form as <see cref="Describe"/>; empty when there is none.</summary>
    private Task<string> StoredSplitAsync(Guid orderId) => StoredAsync<string>(orderId,
        "(SELECT COALESCE(string_agg(p.items, '|' ORDER BY p.position), '') FROM (" +
        "SELECT sp.position, (SELECT string_agg(spl.order_menu_item_id::text, ',' ORDER BY spl.order_menu_item_id::text COLLATE \"C\") " +
        "FROM order_split_part_menu_items spl WHERE spl.part_id = sp.id) AS items " +
        "FROM order_split_parts sp WHERE sp.order_id = o.id) p)");

    private async Task<(Guid OrderId, Guid[] ItemIds)> OrderWithLinesAsync(int count)
    {
        var orderId = await _client.CreateOrderAsync();
        var itemIds = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var menuItemId = await _client.CreateMenuItemAsync($"Side {i}", 1.25m);
            var response = await _client.AddItemAsync(orderId, menuItemId, 1);
            await response.AssertSuccessAsync();
            itemIds.Add((await response.ReadDataAsync()).GetProperty("items").EnumerateArray()
                .Single(item => item.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid());
        }

        return (orderId, [.. itemIds]);
    }

    /// <summary>Fails on any outcome not listed (null is a success); returns the label of every successful request.</summary>
    private static List<string> AssertOnly(
        IReadOnlyList<(ErrorCode? Status, string Label)> results, params ErrorCode?[] allowed)
    {
        var unexpected = results
            .Where(r => !allowed.Contains(r.Status))
            .GroupBy(r => r.Status)
            .Select(g => $"{g.Key} x{g.Count()}")
            .ToList();
        Assert.True(unexpected.Count == 0,
            $"Expected only {string.Join("/", allowed.Select(s => s?.ToString() ?? "success"))}, got: {string.Join(", ", unexpected)}");

        var accepted = results.Where(r => r.Status is null).Select(r => r.Label).ToList();
        Assert.NotEmpty(accepted);
        return accepted;
    }

    /// <summary>Reads straight from Postgres so the API's own view can't hide a lost update.</summary>
    private async Task<T> StoredAsync<T>(Guid orderId, string expression)
    {
        string connectionString;
        using (var scope = factory.Services.CreateScope())
        {
            connectionString = scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.GetConnectionString()!;
        }

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"SELECT {expression} FROM orders o WHERE o.id = @o", conn);
        cmd.Parameters.AddWithValue("o", orderId);
        var value = await cmd.ExecuteScalarAsync();
        return value is DBNull ? default! : (T)value!;
    }
}
