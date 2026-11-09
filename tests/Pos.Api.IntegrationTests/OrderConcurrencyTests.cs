using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Fires parallel requests at one order and checks the stored result: every request either succeeded or was rejected
/// as a conflict, and nothing a success reported is lost.
/// </summary>
public class OrderConcurrencyTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int Requests = 20;
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Parallel_adds_of_the_same_item_never_lose_quantity()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();
        // Create the item first so the parallel requests all update the same row.
        await (await _client.AddItemAsync(orderId, pho, 1)).AssertSuccessAsync();

        var statuses = await FireInParallel(_ => _client.AddItemAsync(orderId, pho, 1));

        Assert.All(statuses, s => Assert.True(s is null or ErrorCode.ConcurrentChange or ErrorCode.DataConflict, $"Unexpected {s}"));
        var succeeded = statuses.Count(s => s is null);
        var order = await _client.GetOrderAsync(orderId);
        var item = Assert.Single(order.GetProperty("items").EnumerateArray());
        Assert.Equal(1 + succeeded, item.GetProperty("quantity").GetInt32());
        Assert.Equal((7.70m * (1 + succeeded)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            order.Money("subtotal"));
    }

    [Fact]
    public async Task Parallel_first_adds_to_an_empty_order_never_lose_items()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var statuses = await FireInParallel(_ => _client.AddItemAsync(orderId, pho, 1));

        Assert.All(statuses, s => Assert.True(s is null or ErrorCode.ConcurrentChange or ErrorCode.DataConflict, $"Unexpected {s}"));
        var succeeded = statuses.Count(s => s is null);
        var items = (await _client.GetOrderAsync(orderId)).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(succeeded, items.Sum(l => l.GetProperty("quantity").GetInt32()));
        Assert.Single(items);
    }

    [Fact]
    public async Task Parallel_adds_of_different_items_never_lose_items()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var statuses = await FireInParallel(i => _client.AddItemAsync(orderId, pho, 1, notes: $"seat {i}"));

        Assert.All(statuses, s => Assert.True(s is null or ErrorCode.ConcurrentChange or ErrorCode.DataConflict, $"Unexpected {s}"));
        var succeeded = statuses.Count(s => s is null);
        var items = (await _client.GetOrderAsync(orderId)).GetProperty("items").GetArrayLength();
        Assert.Equal(succeeded, items);
    }

    [Fact]
    public async Task Parallel_creates_get_distinct_order_numbers()
    {
        var numbers = new System.Collections.Concurrent.ConcurrentBag<int>();

        var statuses = await FireInParallel(async _ =>
        {
            var response = await _client.PostAsync("/api/orders", null);
            if (await response.OutcomeAsync() is null)
            {
                numbers.Add((await response.ReadDataAsync()).GetProperty("number").GetInt32());
            }

            return response;
        });

        Assert.All(statuses, s => Assert.Null(s));
        Assert.Equal(Requests, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.True(n > 0));
    }

    private static async Task<ErrorCode?[]> FireInParallel(Func<int, Task<HttpResponseMessage>> request)
    {
        using var start = new SemaphoreSlim(0);
        var tasks = Enumerable.Range(0, Requests).Select(async i =>
        {
            await start.WaitAsync();
            using var response = await request(i);
            return await response.OutcomeAsync();
        }).ToList();
        start.Release(Requests);
        return await Task.WhenAll(tasks);
    }
}
