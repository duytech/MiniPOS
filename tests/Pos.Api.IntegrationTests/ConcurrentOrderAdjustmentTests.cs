using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Discount, tip, payment method, pay and cancel requests racing each other and add-item requests on the same order.
/// The invariant: no 5xx, every rejected request is a 409 (or a 422 once a racing request closed the order), and the
/// stored row shows exactly the accepted changes.
/// </summary>
public class ConcurrentOrderAdjustmentTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int ConcurrentRequests = 10;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Parallel_tips_store_the_rate_of_an_accepted_request()
    {
        var (orderId, _) = await OrderWithOneItemAsync();

        var results = await ConcurrentlyAsync(Enumerable.Range(1, ConcurrentRequests)
            .Select(rate => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.SetTipAsync(orderId, rate);
                return (response.StatusCode, rate.ToString(CultureInfo.InvariantCulture));
            })));

        var accepted = AssertOnlyOkOrConflict(results);
        Assert.Contains(await StoredAsync<string>(orderId, "tip_rate::int::text"), accepted);
    }

    [Fact]
    public async Task Parallel_discounts_store_the_rate_of_an_accepted_request()
    {
        var (orderId, _) = await OrderWithOneItemAsync();

        var results = await ConcurrentlyAsync(Enumerable.Range(1, ConcurrentRequests)
            .Select(rate => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.ApplyDiscountAsync(orderId, new { rate });
                return (response.StatusCode, rate.ToString(CultureInfo.InvariantCulture));
            })));

        var accepted = AssertOnlyOkOrConflict(results);
        Assert.Contains(await StoredAsync<string>(orderId, "discount_rate::int::text"), accepted);
        Assert.Null(await StoredAsync<decimal?>(orderId, "discount_amount"));
    }

    public static TheoryData<string> Changes => ["apply-discount", "remove-discount", "set-tip", "remove-tip", "card"];

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task Parallel_changes_and_add_items_lose_neither(string change)
    {
        var (orderId, menuItemId) = await OrderWithOneItemAsync();
        if (change == "remove-discount") (await _client.ApplyDiscountAsync(orderId, new { rate = 10 })).EnsureSuccessStatusCode();
        if (change == "remove-tip") (await _client.SetTipAsync(orderId, 15m)).EnsureSuccessStatusCode();

        var requests = Enumerable.Range(0, ConcurrentRequests).SelectMany(_ => new Func<Task<(HttpStatusCode, string)>>[]
        {
            async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (response.StatusCode, "add");
            },
            async () =>
            {
                using var response = await SendChangeAsync(change, orderId);
                return (response.StatusCode, change);
            },
        });
        var results = await ConcurrentlyAsync(requests);

        var accepted = AssertOnlyOkOrConflict(results);
        Assert.Equal(1 + accepted.Count(a => a == "add"), await StoredAsync<int>(
            orderId, "(SELECT SUM(quantity)::int FROM order_lines WHERE order_id = o.id)"));
        if (accepted.Contains(change))
        {
            var (column, expected) = change switch
            {
                "apply-discount" => ("discount_rate::int::text", "10"),
                "remove-discount" => ("COALESCE(discount_rate::text, 'none')", "none"),
                "set-tip" => ("tip_rate::int::text", "18"),
                "remove-tip" => ("COALESCE(tip_rate::text, 'none')", "none"),
                _ => ("payment_method", "Card"),
            };
            Assert.Equal(expected, await StoredAsync<string>(orderId, column));
        }
    }

    [Fact]
    public async Task Parallel_removals_of_different_lines_delete_exactly_the_accepted_lines()
    {
        var orderId = await _client.CreateOrderAsync();
        var lineIds = await AddDistinctLinesAsync(orderId, ConcurrentRequests);

        var results = await ConcurrentlyAsync(lineIds.Select(lineId => (Func<Task<(HttpStatusCode, string)>>)(async () =>
        {
            using var response = await _client.RemoveLineAsync(orderId, lineId);
            return (response.StatusCode, lineId.ToString());
        })));

        var accepted = AssertOnlyOkOrConflict(results);
        var expected = lineIds.Select(id => id.ToString()).Except(accepted).Order(StringComparer.Ordinal);
        Assert.Equal(string.Join(",", expected), await StoredLineIdsAsync(orderId));
    }

    [Fact]
    public async Task Parallel_line_removals_and_add_items_lose_neither()
    {
        var (orderId, menuItemId) = await OrderWithOneItemAsync();
        var removableLineIds = await AddDistinctLinesAsync(orderId, ConcurrentRequests);

        var requests = removableLineIds.SelectMany(lineId => new Func<Task<(HttpStatusCode, string)>>[]
        {
            async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (response.StatusCode, "add");
            },
            async () =>
            {
                using var response = await _client.RemoveLineAsync(orderId, lineId);
                return (response.StatusCode, lineId.ToString());
            },
        });
        var results = await ConcurrentlyAsync(requests);

        var accepted = AssertOnlyOkOrConflict(results);
        Assert.Equal(1 + accepted.Count(a => a == "add"), await StoredAsync<int>(
            orderId, $"(SELECT quantity FROM order_lines WHERE order_id = o.id AND menu_item_id = '{menuItemId}')"));
        var storedLineIds = (await StoredLineIdsAsync(orderId)).Split(',');
        var expectedRemaining = removableLineIds.Select(id => id.ToString()).Except(accepted);
        Assert.Equal(
            expectedRemaining.Order(StringComparer.Ordinal),
            storedLineIds.Where(id => removableLineIds.Contains(Guid.Parse(id))).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Subtotal 11.50 (Pho 9.50 + Tea 2.00). A fixed 5.00 discount and removing Pho are each fine alone, but not both:
    /// whatever the interleaving, the stored discount must never be more than the stored subtotal.
    /// </summary>
    [Fact]
    public async Task Line_removal_racing_a_fixed_discount_never_leaves_the_discount_above_the_subtotal()
    {
        var (orderId, _) = await OrderWithOneItemAsync();
        (await _client.AddItemAsync(orderId, await _client.CreateMenuItemAsync("Tea", 2.00m), 1)).EnsureSuccessStatusCode();
        var phoLineId = Guid.Parse(await StoredAsync<string>(
            orderId, "(SELECT id::text FROM order_lines WHERE order_id = o.id AND unit_price = 9.50)"));

        var requests = Enumerable.Range(0, ConcurrentRequests).Select(_ => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.ApplyDiscountAsync(orderId, new { amount = 5.00m });
                return (response.StatusCode, "discount");
            }))
            .Append(async () =>
            {
                using var response = await _client.RemoveLineAsync(orderId, phoLineId);
                return (response.StatusCode, "remove");
            });
        var results = await ConcurrentlyAsync(requests);

        var unexpected = results.Where(r => r.Item1 is not (HttpStatusCode.OK or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity)).ToList();
        Assert.Empty(unexpected);
        var order = await _client.GetOrderAsync(orderId);
        var removedPho = results.Any(r => r is (HttpStatusCode.OK, "remove"));
        var discounted = results.Any(r => r is (HttpStatusCode.OK, "discount"));
        Assert.Equal(!removedPho, discounted);
        Assert.Equal(removedPho ? "2.00" : "11.50", order.Money("subtotal"));
        Assert.Equal(discounted ? "5.00" : "0.00", order.Money("discount"));
    }

    [Fact]
    public async Task Parallel_quantity_changes_of_different_lines_store_exactly_the_accepted_quantities()
    {
        var orderId = await _client.CreateOrderAsync();
        var lineIds = await AddDistinctLinesAsync(orderId, ConcurrentRequests);

        var results = await ConcurrentlyAsync(lineIds.Select(lineId => (Func<Task<(HttpStatusCode, string)>>)(async () =>
        {
            using var response = await _client.ChangeLineQuantityAsync(orderId, lineId, 5);
            return (response.StatusCode, lineId.ToString());
        })));

        var accepted = AssertOnlyOkOrConflict(results);
        var expected = lineIds
            .Select(id => $"{id}:{(accepted.Contains(id.ToString()) ? 5 : 1)}")
            .Order(StringComparer.Ordinal);
        Assert.Equal(string.Join(",", expected), await StoredLineQuantitiesAsync(orderId));
    }

    [Fact]
    public async Task Parallel_quantity_changes_and_add_items_lose_neither()
    {
        var (orderId, menuItemId) = await OrderWithOneItemAsync();
        var changedLineIds = await AddDistinctLinesAsync(orderId, ConcurrentRequests);

        var requests = changedLineIds.SelectMany(lineId => new Func<Task<(HttpStatusCode, string)>>[]
        {
            async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (response.StatusCode, "add");
            },
            async () =>
            {
                using var response = await _client.ChangeLineQuantityAsync(orderId, lineId, 4);
                return (response.StatusCode, lineId.ToString());
            },
        });
        var results = await ConcurrentlyAsync(requests);

        var accepted = AssertOnlyOkOrConflict(results);
        Assert.Equal(1 + accepted.Count(a => a == "add"), await StoredAsync<int>(
            orderId, $"(SELECT quantity FROM order_lines WHERE order_id = o.id AND menu_item_id = '{menuItemId}')"));
        var expected = changedLineIds
            .Select(id => $"{id}:{(accepted.Contains(id.ToString()) ? 4 : 1)}")
            .Order(StringComparer.Ordinal);
        var stored = (await StoredLineQuantitiesAsync(orderId)).Split(',')
            .Where(entry => changedLineIds.Contains(Guid.Parse(entry.Split(':')[0])));
        Assert.Equal(expected, stored.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Bun cha 9.50 × 2 = 19.00. A fixed 10.00 discount and lowering Bun cha to 1 (subtotal 9.50) are each fine alone,
    /// but not both: whatever the interleaving, the stored discount must never be more than the stored subtotal.
    /// </summary>
    [Fact]
    public async Task Quantity_decrease_racing_a_fixed_discount_never_leaves_the_discount_above_the_subtotal()
    {
        var (orderId, menuItemId) = await OrderWithOneItemAsync();
        (await _client.AddItemAsync(orderId, menuItemId, 1)).EnsureSuccessStatusCode();
        var lineId = Guid.Parse(await StoredAsync<string>(
            orderId, "(SELECT id::text FROM order_lines WHERE order_id = o.id)"));

        var requests = Enumerable.Range(0, ConcurrentRequests).Select(_ => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.ApplyDiscountAsync(orderId, new { amount = 10.00m });
                return (response.StatusCode, "discount");
            }))
            .Append(async () =>
            {
                using var response = await _client.ChangeLineQuantityAsync(orderId, lineId, 1);
                return (response.StatusCode, "decrease");
            });
        var results = await ConcurrentlyAsync(requests);

        var unexpected = results.Where(r => r.Item1 is not (HttpStatusCode.OK or HttpStatusCode.Conflict
            or HttpStatusCode.UnprocessableEntity)).ToList();
        Assert.Empty(unexpected);
        var order = await _client.GetOrderAsync(orderId);
        var decreased = results.Any(r => r is (HttpStatusCode.OK, "decrease"));
        var discounted = results.Any(r => r is (HttpStatusCode.OK, "discount"));
        Assert.Equal(!decreased, discounted);
        Assert.Equal(decreased ? "9.50" : "19.00", order.Money("subtotal"));
        Assert.Equal(discounted ? "10.00" : "0.00", order.Money("discount"));
    }

    /// <summary>
    /// Every pay would succeed alone; together exactly one wins. The others see the paid order (422) or lose the race on
    /// the concurrency token (409).
    /// </summary>
    [Fact]
    public async Task Parallel_pays_accept_exactly_one()
    {
        var (orderId, _) = await OrderWithOneItemAsync();

        var results = await ConcurrentlyAsync(Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.PayOrderAsync(orderId);
                return (response.StatusCode, "pay");
            })));

        var accepted = AssertOnlyOkConflictOrUnprocessable(results);
        Assert.Single(accepted);
        Assert.Contains(results, r => r.Item1 == HttpStatusCode.Conflict); // the requests really raced
        Assert.Equal("Paid", await StoredAsync<string>(orderId, "status"));
        Assert.True(await StoredAsync<bool>(orderId, "paid_at IS NOT NULL"));
        Assert.True(await StoredAsync<bool>(orderId, "cancelled_at IS NULL"));
    }

    [Fact]
    public async Task Parallel_pays_and_cancels_accept_exactly_one_and_store_its_status()
    {
        var (orderId, _) = await OrderWithOneItemAsync();

        var results = await ConcurrentlyAsync(Enumerable.Range(0, ConcurrentRequests / 2)
            .SelectMany(_ => new Func<Task<(HttpStatusCode, string)>>[]
            {
                async () =>
                {
                    using var response = await _client.PayOrderAsync(orderId);
                    return (response.StatusCode, "Paid");
                },
                async () =>
                {
                    using var response = await _client.CancelOrderAsync(orderId);
                    return (response.StatusCode, "Cancelled");
                },
            }));

        var winner = Assert.Single(AssertOnlyOkConflictOrUnprocessable(results));
        Assert.Equal(winner, await StoredAsync<string>(orderId, "status"));
        Assert.Equal(winner == "Paid", await StoredAsync<bool>(orderId, "paid_at IS NOT NULL"));
        Assert.Equal(winner == "Cancelled", await StoredAsync<bool>(orderId, "cancelled_at IS NOT NULL"));
    }

    /// <summary>Adds that commit before the pay are kept; adds that load the paid order are 422; nothing is lost.</summary>
    [Fact]
    public async Task Pay_racing_add_items_keeps_exactly_the_accepted_adds()
    {
        var (orderId, menuItemId) = await OrderWithOneItemAsync();

        var requests = Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => (Func<Task<(HttpStatusCode, string)>>)(async () =>
            {
                using var response = await _client.AddItemAsync(orderId, menuItemId, 1);
                return (response.StatusCode, "add");
            }))
            .Append(async () =>
            {
                using var response = await _client.PayOrderAsync(orderId);
                return (response.StatusCode, "pay");
            });
        var results = await ConcurrentlyAsync(requests);

        var accepted = AssertOnlyOkConflictOrUnprocessable(results);
        Assert.Equal(1 + accepted.Count(a => a == "add"), await StoredAsync<int>(
            orderId, "(SELECT SUM(quantity)::int FROM order_lines WHERE order_id = o.id)"));
        Assert.Equal(accepted.Contains("pay") ? "Paid" : "Open", await StoredAsync<string>(orderId, "status"));
        Assert.Equal(accepted.Contains("pay"), await StoredAsync<bool>(orderId, "paid_at IS NOT NULL"));
    }

    private Task<string> StoredLineQuantitiesAsync(Guid orderId) => StoredAsync<string>(
        orderId, "(SELECT COALESCE(string_agg(id::text || ':' || quantity, ',' ORDER BY id::text COLLATE \"C\"), '') " +
                 "FROM order_lines WHERE order_id = o.id)");

    private async Task<List<Guid>> AddDistinctLinesAsync(Guid orderId, int count)
    {
        var lineIds = new List<Guid>();
        for (var i = 0; i < count; i++)
        {
            var menuItemId = await _client.CreateMenuItemAsync($"Side {i}", 1.25m);
            var response = await _client.AddItemAsync(orderId, menuItemId, 1);
            response.EnsureSuccessStatusCode();
            lineIds.Add((await response.ReadJsonAsync()).GetProperty("lines").EnumerateArray()
                .Single(l => l.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid());
        }

        return lineIds;
    }

    private Task<string> StoredLineIdsAsync(Guid orderId) => StoredAsync<string>(
        orderId, "(SELECT COALESCE(string_agg(id::text, ',' ORDER BY id::text COLLATE \"C\"), '') FROM order_lines WHERE order_id = o.id)");

    private Task<HttpResponseMessage> SendChangeAsync(string change, Guid orderId) => change switch
    {
        "apply-discount" => _client.ApplyDiscountAsync(orderId, new { rate = 10 }),
        "remove-discount" => _client.RemoveDiscountAsync(orderId),
        "set-tip" => _client.SetTipAsync(orderId, 18m),
        "remove-tip" => _client.RemoveTipAsync(orderId),
        _ => _client.SetPaymentMethodAsync(orderId, "Card"),
    };

    private async Task<(Guid OrderId, Guid MenuItemId)> OrderWithOneItemAsync()
    {
        var menuItemId = await _client.CreateMenuItemAsync("Bun cha", 9.50m);
        var orderId = await _client.CreateOrderAsync();
        (await _client.AddItemAsync(orderId, menuItemId, 1)).EnsureSuccessStatusCode();
        return (orderId, menuItemId);
    }

    /// <summary>Returns the label of every accepted (200) request.</summary>
    private static List<string> AssertOnlyOkOrConflict(IReadOnlyList<(HttpStatusCode Status, string Label)> results)
    {
        var unexpected = results
            .Where(r => r.Status is not (HttpStatusCode.OK or HttpStatusCode.Conflict))
            .GroupBy(r => r.Status)
            .Select(g => $"{(int)g.Key} x{g.Count()}")
            .ToList();
        Assert.True(unexpected.Count == 0, $"Expected only 200 or 409, got: {string.Join(", ", unexpected)}");

        var accepted = results.Where(r => r.Status == HttpStatusCode.OK).Select(r => r.Label).ToList();
        Assert.NotEmpty(accepted);
        return accepted;
    }

    /// <summary>
    /// For races where a request can legitimately find the order already closed (422). Returns the label of every
    /// accepted (200) request.
    /// </summary>
    private static List<string> AssertOnlyOkConflictOrUnprocessable(
        IReadOnlyList<(HttpStatusCode Status, string Label)> results)
    {
        var unexpected = results
            .Where(r => r.Status is not (HttpStatusCode.OK or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity))
            .GroupBy(r => r.Status)
            .Select(g => $"{(int)g.Key} x{g.Count()}")
            .ToList();
        Assert.True(unexpected.Count == 0, $"Expected only 200, 409 or 422, got: {string.Join(", ", unexpected)}");

        var accepted = results.Where(r => r.Status == HttpStatusCode.OK).Select(r => r.Label).ToList();
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
