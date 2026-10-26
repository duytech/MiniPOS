using System.Net;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Fires parallel requests at one order and checks the stored result: every request either succeeded (200) or was
/// rejected as a conflict (409), and nothing a 200 reported is lost.
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
        // Create the line first so the parallel requests all update the same row.
        Assert.Equal(HttpStatusCode.OK, (await _client.AddItemAsync(orderId, pho, 1)).StatusCode);

        var statuses = await FireInParallel(_ => _client.AddItemAsync(orderId, pho, 1));

        Assert.All(statuses, s => Assert.Contains(s, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var succeeded = statuses.Count(s => s == HttpStatusCode.OK);
        var order = await _client.GetOrderAsync(orderId);
        var line = Assert.Single(order.GetProperty("lines").EnumerateArray());
        Assert.Equal(1 + succeeded, line.GetProperty("quantity").GetInt32());
        Assert.Equal((7.70m * (1 + succeeded)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            order.Money("total"));
    }

    [Fact]
    public async Task Parallel_first_adds_to_an_empty_order_never_lose_lines()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var statuses = await FireInParallel(_ => _client.AddItemAsync(orderId, pho, 1));

        Assert.All(statuses, s => Assert.Contains(s, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var succeeded = statuses.Count(s => s == HttpStatusCode.OK);
        var lines = (await _client.GetOrderAsync(orderId)).GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(succeeded, lines.Sum(l => l.GetProperty("quantity").GetInt32()));
        Assert.Single(lines);
    }

    [Fact]
    public async Task Parallel_adds_of_different_lines_never_lose_lines()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var statuses = await FireInParallel(i => _client.AddItemAsync(orderId, pho, 1, notes: $"seat {i}"));

        Assert.All(statuses, s => Assert.Contains(s, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        var succeeded = statuses.Count(s => s == HttpStatusCode.OK);
        var lines = (await _client.GetOrderAsync(orderId)).GetProperty("lines").GetArrayLength();
        Assert.Equal(succeeded, lines);
    }

    [Fact]
    public async Task Parallel_creates_get_distinct_order_numbers()
    {
        var numbers = new System.Collections.Concurrent.ConcurrentBag<int>();

        var statuses = await FireInParallel(async _ =>
        {
            var response = await _client.PostAsync("/api/orders", null);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                numbers.Add((await response.ReadJsonAsync()).GetProperty("number").GetInt32());
            }

            return response;
        });

        Assert.All(statuses, s => Assert.Equal(HttpStatusCode.Created, s));
        Assert.Equal(Requests, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.True(n > 0));
    }

    private static async Task<HttpStatusCode[]> FireInParallel(Func<int, Task<HttpResponseMessage>> request)
    {
        using var start = new SemaphoreSlim(0);
        var tasks = Enumerable.Range(0, Requests).Select(async i =>
        {
            await start.WaitAsync();
            using var response = await request(i);
            return response.StatusCode;
        }).ToList();
        start.Release(Requests);
        return await Task.WhenAll(tasks);
    }
}
