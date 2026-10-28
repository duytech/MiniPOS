using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Saving and removing a split go through the order's concurrency token (the orders row is touched), so they race
/// each other and add-item like any order change. The invariant: no 5xx, and the stored split rows are exactly one
/// accepted request's split, or none, and never name lines the order does not have or miss one it has.
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

        var results = await ConcurrentlyAsync(groupings.Select(grouping => (Func<Task<(HttpStatusCode, string)>>)(async () =>
        {
            var parts = grouping.Select(part => part.Select(i => l[i])).ToArray();
            using var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody(parts));
            return (response.StatusCode, Describe(parts));
        })));

        var accepted = AssertOnly(results, HttpStatusCode.OK, HttpStatusCode.Conflict);
        Assert.Contains(results, r => r.Item1 == HttpStatusCode.Conflict); // the requests really raced
        Assert.Contains(await StoredSplitAsync(orderId), accepted);
    }

    /// <summary>
    /// Every PUT names the first four lines only. A PUT that loads the order after an add committed misses the new line
    /// (422); one that loses the race on the token is 409. Whatever wins, no add is lost and a stored split covers
    /// exactly the stored lines.
    /// </summary>
    [Fact]
    public async Task Puts_racing_add_items_lose_no_line_and_never_store_a_stale_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(4);
        var newMenuItemIds = new List<Guid>();
        for (var i = 0; i < ConcurrentRequests; i++)
        {
            newMenuItemIds.Add(await _client.CreateMenuItemAsync($"Extra {i}", 2.00m));
        }

        object[] bodies = [ApiClient.SplitBody([l[0], l[1]], [l[2], l[3]]), ApiClient.SplitBody([l[0]], [l[1], l[2], l[3]])];

        var requests = newMenuItemIds.SelectMany((menuItemId, i) => new Func<Task<(HttpStatusCode, string)>>[]
        {
            async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (response.StatusCode, "add");
            },
            async () =>
            {
                using var response = await _client.SaveSplitAsync(orderId, bodies[i % 2]);
                return (response.StatusCode, "put");
            },
        });
        var results = await ConcurrentlyAsync(requests);

        AssertOnly(results, HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.UnprocessableEntity);
        Assert.Contains(results, r => r.Item1 == HttpStatusCode.Conflict);
        var acceptedAdds = results.Count(r => r is (HttpStatusCode.OK, "add"));
        Assert.Equal(4 + acceptedAdds, await StoredAsync<int>(
            orderId, "(SELECT COUNT(*)::int FROM order_lines WHERE order_id = o.id)"));
        var storedSplitLines = await StoredAsync<string>(orderId,
            "(SELECT COALESCE(string_agg(spl.line_id::text, ',' ORDER BY spl.line_id::text COLLATE \"C\"), '') " +
            "FROM order_split_parts sp JOIN order_split_part_lines spl ON spl.part_id = sp.id WHERE sp.order_id = o.id)");
        if (storedSplitLines.Length > 0)
        {
            Assert.Equal(await StoredAsync<string>(orderId,
                "(SELECT string_agg(id::text, ',' ORDER BY id::text COLLATE \"C\") FROM order_lines WHERE order_id = o.id)"),
                storedSplitLines);
        }
    }

    /// <summary>
    /// The order starts with a saved split; DELETEs race PUTs of a different split. A DELETE that loads the order after
    /// another DELETE committed finds no split (404). The stored split is the PUT's or none, matching who was accepted.
    /// </summary>
    [Fact]
    public async Task Parallel_deletes_and_puts_leave_a_consistent_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(3);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1], l[2]]))).EnsureSuccessStatusCode();
        IEnumerable<Guid>[] putParts = [[l[2]], [l[0], l[1]]];

        var requests = Enumerable.Range(0, ConcurrentRequests / 2).SelectMany(_ => new Func<Task<(HttpStatusCode, string)>>[]
        {
            async () =>
            {
                using var response = await _client.RemoveSplitAsync(orderId);
                return (response.StatusCode, "delete");
            },
            async () =>
            {
                using var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody(putParts));
                return (response.StatusCode, "put");
            },
        });
        var results = await ConcurrentlyAsync(requests);

        AssertOnly(results, HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        Assert.Contains(results, r => r.Item1 == HttpStatusCode.Conflict);
        Assert.All(results.Where(r => r.Item2 == "put"), r => Assert.NotEqual(HttpStatusCode.NotFound, r.Item1));
        Assert.All(results.Where(r => r.Item2 == "delete"), r => Assert.NotEqual(HttpStatusCode.OK, r.Item1));
        var stored = await StoredSplitAsync(orderId);
        if (stored.Length == 0)
        {
            Assert.Contains(results, r => r is (HttpStatusCode.NoContent, "delete"));
        }
        else
        {
            Assert.Equal(Describe(putParts), stored);
            Assert.Contains(results, r => r is (HttpStatusCode.OK, "put"));
        }
    }

    /// <summary>Parts in order, separated by '|'; the line ids of a part sorted (line order in a part is not stored).</summary>
    private static string Describe(IEnumerable<IEnumerable<Guid>> parts) =>
        string.Join("|", parts.Select(part => string.Join(",", part.Select(id => id.ToString()).Order(StringComparer.Ordinal))));

    /// <summary>The stored split in the same form as <see cref="Describe"/>; empty when there is none.</summary>
    private Task<string> StoredSplitAsync(Guid orderId) => StoredAsync<string>(orderId,
        "(SELECT COALESCE(string_agg(p.lines, '|' ORDER BY p.position), '') FROM (" +
        "SELECT sp.position, (SELECT string_agg(spl.line_id::text, ',' ORDER BY spl.line_id::text COLLATE \"C\") " +
        "FROM order_split_part_lines spl WHERE spl.part_id = sp.id) AS lines " +
        "FROM order_split_parts sp WHERE sp.order_id = o.id) p)");

    private async Task<(Guid OrderId, Guid[] LineIds)> OrderWithLinesAsync(int count)
    {
        var orderId = await _client.CreateOrderAsync();
        var lineIds = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var menuItemId = await _client.CreateMenuItemAsync($"Side {i}", 1.25m);
            var response = await _client.AddItemAsync(orderId, menuItemId, 1);
            response.EnsureSuccessStatusCode();
            lineIds.Add((await response.ReadJsonAsync()).GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid());
        }

        return (orderId, [.. lineIds]);
    }

    /// <summary>Fails on any status not listed; returns the label of every accepted (2xx) request.</summary>
    private static List<string> AssertOnly(
        IReadOnlyList<(HttpStatusCode Status, string Label)> results, params HttpStatusCode[] allowed)
    {
        var unexpected = results
            .Where(r => !allowed.Contains(r.Status))
            .GroupBy(r => r.Status)
            .Select(g => $"{(int)g.Key} x{g.Count()}")
            .ToList();
        Assert.True(unexpected.Count == 0,
            $"Expected only {string.Join("/", allowed.Select(s => (int)s))}, got: {string.Join(", ", unexpected)}");

        var accepted = results.Where(r => (int)r.Status is >= 200 and < 300).Select(r => r.Label).ToList();
        Assert.NotEmpty(accepted);
        return accepted;
    }

    /// <summary>Releases all requests at once so they hit the same order row together.</summary>
    private static async Task<IReadOnlyList<(HttpStatusCode, string)>> ConcurrentlyAsync(
        IEnumerable<Func<Task<(HttpStatusCode, string)>>> requests)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = requests
            .Select(async send =>
            {
                await start.Task;
                return await send();
            })
            .ToList();

        start.SetResult();
        return await Task.WhenAll(running);
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
