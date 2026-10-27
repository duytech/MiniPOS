using System.Globalization;
using System.Net;
using System.Text.Json;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// <c>GET /api/orders/{id}/split-even?parts=N</c>: each amount divided by N, rounded down to the cent, leftover cents
/// to the first part. Pricing config: tax 8.875%, card surcharge 3%.
/// </summary>
public class SplitEvenApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Leftover_cents_go_to_the_first_part()
    {
        var orderId = await OrderWithSubtotalAsync(100.00m); // tax 8.875% of 100.00 = 8.875 → 8.88, total 108.88

        var parts = await SplitAsync(orderId, 3);

        Assert.Equal(["33.34", "33.33", "33.33"], parts.Select(p => p.Money("subtotal")));
        Assert.Equal(["2.96", "2.96", "2.96"], parts.Select(p => p.Money("tax")));
        Assert.Equal(["36.30", "36.29", "36.29"], parts.Select(p => p.Money("total")));
        await AssertPartTotalsAddUpToGetTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task Discount_surcharge_tax_and_tip_are_each_divided_separately()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);
        await _client.ApplyDiscountAsync(orderId, new { rate = 15 });
        await _client.SetPaymentMethodAsync(orderId, "Card");
        await _client.SetTipAsync(orderId, 18);
        // Order: subtotal 10.00, discount 1.50, surcharge 0.26 (3% of 8.50 = 0.255), tax 0.78, tip 1.53, total 11.07.

        var parts = await SplitAsync(orderId, 4);

        Assert.Equal(["2.50", "2.50", "2.50", "2.50"], parts.Select(p => p.Money("subtotal")));
        Assert.Equal(["0.39", "0.37", "0.37", "0.37"], parts.Select(p => p.Money("discount"))); // 1.50 / 4 = 0.375
        Assert.Equal(["0.08", "0.06", "0.06", "0.06"], parts.Select(p => p.Money("surcharge"))); // 0.26 / 4 = 0.065
        Assert.Equal(["0.21", "0.19", "0.19", "0.19"], parts.Select(p => p.Money("tax"))); // 0.78 / 4 = 0.195
        Assert.Equal(["0.39", "0.38", "0.38", "0.38"], parts.Select(p => p.Money("tip"))); // 1.53 / 4 = 0.3825
        Assert.Equal(["2.79", "2.76", "2.76", "2.76"], parts.Select(p => p.Money("total")));
        await AssertPartTotalsAddUpToGetTotalAsync(orderId, parts);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    public async Task Parts_below_one_or_not_a_number_is_400(string parts)
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.GetAsync($"/api/orders/{orderId}/split-even?parts={parts}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Missing_parts_is_400()
    {
        var orderId = await OrderWithSubtotalAsync(10.00m);

        var response = await _client.GetAsync($"/api/orders/{orderId}/split-even");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_is_404()
    {
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}/split-even?parts=2");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<JsonElement[]> SplitAsync(Guid orderId, int parts)
    {
        var response = await _client.GetAsync($"/api/orders/{orderId}/split-even?parts={parts}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. (await response.ReadJsonAsync()).EnumerateArray()];
    }

    private async Task<Guid> OrderWithSubtotalAsync(decimal price)
    {
        var menuItem = await _client.CreateMenuItemAsync("Pho", price);
        var orderId = await _client.CreateOrderAsync();
        (await _client.AddItemAsync(orderId, menuItem, 1)).EnsureSuccessStatusCode();
        return orderId;
    }

    /// <summary>Adds up the returned part totals in the test only, to compare with the order total.</summary>
    private async Task AssertPartTotalsAddUpToGetTotalAsync(Guid orderId, JsonElement[] parts)
    {
        var order = await _client.GetOrderAsync(orderId);
        var sum = parts.Sum(p => decimal.Parse(p.Money("total"), CultureInfo.InvariantCulture));
        Assert.Equal(order.Money("total"), sum.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
