using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Tests share one database, so each test names its menu items with its own token and searches for it to see only its
/// own orders.
/// </summary>
public class OrdersListApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_total_equals_get_order_total_with_two_decimals()
    {
        var token = NewToken();
        var pho = await _client.CreateMenuItemAsync($"Pho {token}", 7.70m);
        var tea = await _client.CreateMenuItemAsync($"Tea {token}", 1.15m);
        var orderId = await _client.CreateOrderAsync();
        await _client.AddItemAsync(orderId, pho, 2);
        await _client.AddItemAsync(orderId, tea, 3);

        var list = await ListAsync($"search={token}");

        var listed = Assert.Single(list.GetProperty("items").EnumerateArray());
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal("20.52", listed.Money("total")); // 18.85 + 8.875% tax (1.67)
        Assert.Equal(order.Money("total"), listed.Money("total"));
        Assert.Equal(order.GetProperty("number").GetInt32(), listed.GetProperty("number").GetInt32());
        Assert.Equal("Open", listed.GetProperty("status").GetString());
        Assert.Equal([$"Pho {token}", $"Tea {token}"],
            listed.GetProperty("itemNames").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task Search_matches_item_names_case_insensitively()
    {
        var token = NewToken();
        var orderId = await CreateOrderWithItemAsync($"Iced Coffee {token}");
        await CreateOrderWithItemAsync($"Green Tea {token}");

        var list = await ListAsync($"search=ICED%20coffee%20{token.ToUpperInvariant()}");

        Assert.Equal(1, list.GetProperty("totalCount").GetInt32());
        Assert.Equal(orderId, list.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Search_wildcards_match_themselves()
    {
        var token = NewToken();
        var underscore = await CreateOrderWithItemAsync($"a_b {token}");
        await CreateOrderWithItemAsync($"axb {token}");
        await CreateOrderWithItemAsync($"100 {token}");

        var byUnderscore = await ListAsync($"search=a_b%20{token}");
        var byPercent = await ListAsync($"search={Uri.EscapeDataString($"% {token}")}");

        Assert.Equal(underscore, Assert.Single(byUnderscore.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, byPercent.GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("#{0}")]
    [InlineData("{0}")]
    [InlineData(" #{0} ")]
    public async Task Search_matches_the_order_number(string format)
    {
        var orderId = await _client.CreateOrderAsync();
        var number = (await _client.GetOrderAsync(orderId)).GetProperty("number").GetInt32();

        var list = await ListAsync($"search={Uri.EscapeDataString(string.Format(format, number))}&pageSize=100");

        Assert.Contains(list.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);
    }

    [Fact]
    public async Task Status_filter_combines_with_search()
    {
        var token = NewToken();
        var open = await CreateOrderWithItemAsync($"Pho {token}");
        var paid = await CreateOrderWithItemAsync($"Pho {token}");
        await SetStatusAsync(paid, "Paid");

        var paidList = await ListAsync($"search={token}&status=Paid");
        var openList = await ListAsync($"search={token}&status=Open");
        var cancelledList = await ListAsync($"search={token}&status=Cancelled");
        var allList = await ListAsync($"search={token}");

        Assert.Equal(paid, Assert.Single(paidList.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal("Paid", paidList.GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(open, Assert.Single(openList.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, cancelledList.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, allList.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Pages_are_newest_first_and_total_count_covers_all_pages()
    {
        var token = NewToken();
        var menuItem = await _client.CreateMenuItemAsync($"Pho {token}", 7.70m);
        var created = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var orderId = await _client.CreateOrderAsync();
            await _client.AddItemAsync(orderId, menuItem, 1);
            created.Add(orderId);
        }

        var page1 = await ListAsync($"search={token}&page=1&pageSize=2");
        var page3 = await ListAsync($"search={token}&page=3&pageSize=2");
        var page4 = await ListAsync($"search={token}&page=4&pageSize=2");

        Assert.Equal(5, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page1.GetProperty("page").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        Assert.Equal([created[4], created[3]], Ids(page1));
        Assert.Equal([created[0]], Ids(page3));
        Assert.Empty(Ids(page4));
        Assert.Equal(5, page4.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Default_page_size_is_20()
    {
        var list = await ListAsync("");

        Assert.Equal(1, list.GetProperty("page").GetInt32());
        Assert.Equal(20, list.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("status=Bogus")]
    [InlineData("status=7")]
    [InlineData("page=2147483647&pageSize=100")]
    public async Task Invalid_list_parameters_are_400(string queryString)
    {
        var response = await _client.GetAsync($"/api/orders?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    private async Task<JsonElement> ListAsync(string queryString)
    {
        var response = await _client.GetAsync($"/api/orders?{queryString}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    private async Task<Guid> CreateOrderWithItemAsync(string itemName)
    {
        var menuItem = await _client.CreateMenuItemAsync(itemName, 5.00m);
        var orderId = await _client.CreateOrderAsync();
        Assert.Equal(HttpStatusCode.OK, (await _client.AddItemAsync(orderId, menuItem, 1)).StatusCode);
        return orderId;
    }

    /// <summary>There is no endpoint that changes the status yet, so tests set it in the database.</summary>
    private async Task SetStatusAsync(Guid orderId, string status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        await db.Database.ExecuteSqlAsync($"UPDATE orders SET status = {status} WHERE id = {orderId}");
    }

    private static List<Guid> Ids(JsonElement list) =>
        list.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToList();

    /// <summary>Letters only, so a search for it never also matches an order number.</summary>
    private static string NewToken() =>
        new(Guid.NewGuid().ToString("N").Select(c => (char)('k' + Convert.ToInt32(c.ToString(), 16))).ToArray());
}
