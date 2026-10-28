using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Split by item: <c>GET|PUT|DELETE /api/orders/{id}/split</c> and <c>POST /api/orders/{id}/split/preview</c>. Only the
/// line → part assignment is stored; amounts are recomputed on every read. Pricing config: tax 8.875%, card surcharge 3%.
/// </summary>
public class SplitByItemApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const string EmptyId = "00000000-0000-0000-0000-000000000000";

    private static readonly string[] Amounts = ["subtotal", "discount", "surcharge", "tax", "tip", "total"];

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Saved_split_returns_exact_cents_with_the_leftover_cent_on_the_largest_part()
    {
        var (orderId, l) = await OrderWithLinesAsync(60.00m, 40.00m);

        var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Order tax 8.875% of 100.00 = 8.875 → 8.88; shares 5.328 → 5.32 and 3.552 → 3.55, leftover 0.01 to the 60.00 part.
        var parts = Parts(await response.ReadJsonAsync());
        Assert.Equal(["60.00", "40.00"], parts.Select(p => p.Money("subtotal")));
        Assert.Equal(["0.00", "0.00"], parts.Select(p => p.Money("discount")));
        Assert.Equal(["0.00", "0.00"], parts.Select(p => p.Money("surcharge")));
        Assert.Equal(["5.33", "3.55"], parts.Select(p => p.Money("tax")));
        Assert.Equal(["0.00", "0.00"], parts.Select(p => p.Money("tip")));
        Assert.Equal(["65.33", "43.55"], parts.Select(p => p.Money("total")));
        Assert.Equal([[l[0]], [l[1]]], parts.Select(LineIds));
        await AssertPartTotalsAddUpToGetTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task Discount_surcharge_tax_and_tip_are_shared_by_subtotal()
    {
        var (orderId, l) = await OrderWithLinesAsync(6.00m, 4.00m);
        (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).EnsureSuccessStatusCode();
        (await _client.SetPaymentMethodAsync(orderId, "Card")).EnsureSuccessStatusCode();
        (await _client.SetTipAsync(orderId, 18)).EnsureSuccessStatusCode();
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();

        var parts = Parts(await GetSplitOkAsync(orderId));

        // Order: subtotal 10.00, discount 1.50, surcharge 0.26, tax 0.78, tip 1.53, total 11.07.
        var order = await _client.GetOrderAsync(orderId);
        Assert.Equal(["10.00", "1.50", "0.26", "0.78", "1.53", "11.07"],
            Amounts.Select(amount => order.Money(amount)));
        Assert.Equal(["6.00", "4.00"], parts.Select(p => p.Money("subtotal")));
        Assert.Equal(["0.90", "0.60"], parts.Select(p => p.Money("discount")));
        Assert.Equal(["0.16", "0.10"], parts.Select(p => p.Money("surcharge")));
        Assert.Equal(["0.47", "0.31"], parts.Select(p => p.Money("tax")));
        Assert.Equal(["0.92", "0.61"], parts.Select(p => p.Money("tip")));
        Assert.Equal(["6.65", "4.42"], parts.Select(p => p.Money("total")));
        await AssertPartTotalsAddUpToGetTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task Get_after_put_returns_the_saved_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m, 3.00m);
        var put = await (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0], l[2]], [l[1]]))).ReadJsonAsync();

        var get = await GetSplitOkAsync(orderId);

        AssertSameSplit(put, get);
        Assert.Equal([LineSet(l[0], l[2]), LineSet(l[1])], Parts(get).Select(p => LineSet(LineIds(p))));
    }

    [Fact]
    public async Task A_second_put_replaces_the_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m, 3.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1], l[2]]))).EnsureSuccessStatusCode();

        var put = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[2]], [l[1]], [l[0]]));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var parts = Parts(await GetSplitOkAsync(orderId));
        Assert.Equal([[l[2]], [l[1]], [l[0]]], parts.Select(LineIds));
        Assert.Equal(["3.00", "2.00", "1.00"], parts.Select(p => p.Money("subtotal")));
    }

    [Fact]
    public async Task No_saved_split_is_404()
    {
        var (orderId, _) = await OrderWithLinesAsync(1.00m, 2.00m);

        var response = await _client.GetSplitAsync(orderId);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Saved_split_can_be_read_after_the_order_is_closed(string status)
    {
        var (orderId, l) = await OrderWithLinesAsync(60.00m, 40.00m);
        var put = await (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).ReadJsonAsync();

        await _client.CloseOrderAsync(orderId, status);

        var get = await GetSplitOkAsync(orderId);
        AssertSameSplit(put, get);
        Assert.Equal(["65.33", "43.55"], Parts(get).Select(p => p.Money("total")));
    }

    [Fact]
    public async Task Preview_returns_the_amounts_and_saves_nothing()
    {
        var (orderId, l) = await OrderWithLinesAsync(60.00m, 40.00m);

        var response = await _client.PreviewSplitAsync(orderId, ApiClient.SplitBody([l[1]], [l[0]]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = Parts(await response.ReadJsonAsync());
        Assert.Equal([[l[1]], [l[0]]], parts.Select(LineIds));
        Assert.Equal(["3.55", "5.33"], parts.Select(p => p.Money("tax")));
        Assert.Equal(["43.55", "65.33"], parts.Select(p => p.Money("total")));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Preview_does_not_replace_a_saved_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(60.00m, 40.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();

        (await _client.PreviewSplitAsync(orderId, ApiClient.SplitBody([l[1]], [l[0]]))).EnsureSuccessStatusCode();

        Assert.Equal([[l[0]], [l[1]]], Parts(await GetSplitOkAsync(orderId)).Select(LineIds));
    }

    public static TheoryData<string> BadShapes => ["no-body", "parts-null", "part-null", "line-ids-null", "line-id-empty"];

    [Theory]
    [MemberData(nameof(BadShapes))]
    public async Task Bad_body_shape_is_400_for_put_and_preview(string shape)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        var json = shape switch
        {
            "no-body" => "",
            "parts-null" => """{ "parts": null }""",
            "part-null" => $$"""{ "parts": [ { "lineIds": ["{{l[0]}}"] }, null ] }""",
            "line-ids-null" => $$"""{ "parts": [ { "lineIds": ["{{l[0]}}"] }, { "lineIds": null } ] }""",
            _ => $$"""{ "parts": [ { "lineIds": ["{{l[0]}}"] }, { "lineIds": ["{{l[1]}}", "{{EmptyId}}"] } ] }""",
        };

        var put = await _client.PutAsync($"/api/orders/{orderId}/split", Json(json));
        var preview = await _client.PostAsync($"/api/orders/{orderId}/split/preview", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Empty_order_id_is_400_on_every_split_route()
    {
        var body = ApiClient.SplitBody([Guid.NewGuid()], [Guid.NewGuid()]);

        Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/orders/{EmptyId}/split")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.SaveSplitAsync(Guid.Empty, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PreviewSplitAsync(Guid.Empty, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.RemoveSplitAsync(Guid.Empty)).StatusCode);
    }

    [Fact]
    public async Task Unknown_order_is_404_on_every_split_route()
    {
        var orderId = Guid.NewGuid();
        var body = ApiClient.SplitBody([Guid.NewGuid()], [Guid.NewGuid()]);
        var detail = $"Order '{orderId}' was not found.";

        await AssertProblemAsync(await _client.GetSplitAsync(orderId), HttpStatusCode.NotFound, detail);
        await AssertProblemAsync(await _client.SaveSplitAsync(orderId, body), HttpStatusCode.NotFound, detail);
        await AssertProblemAsync(await _client.PreviewSplitAsync(orderId, body), HttpStatusCode.NotFound, detail);
        await AssertProblemAsync(await _client.RemoveSplitAsync(orderId), HttpStatusCode.NotFound, detail);
    }

    public static TheoryData<string> BrokenSplits =>
        ["one-part", "no-parts", "empty-part", "unknown-line", "duplicate-line", "missing-line"];

    [Theory]
    [MemberData(nameof(BrokenSplits))]
    public async Task Broken_split_is_422_with_the_rule_for_put_and_preview(string broken)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m, 3.00m);
        var unknown = Guid.NewGuid();
        var (body, detail) = broken switch
        {
            "one-part" => (ApiClient.SplitBody([l[0], l[1], l[2]]), "A split needs at least two parts."),
            "no-parts" => (ApiClient.SplitBody(), "A split needs at least two parts."),
            "empty-part" => (ApiClient.SplitBody([l[0], l[1], l[2]], []), "Every part of a split needs at least one line."),
            "unknown-line" => (ApiClient.SplitBody([l[0], l[1]], [l[2], unknown]), $"Line {unknown} is not on this order."),
            "duplicate-line" => (ApiClient.SplitBody([l[0], l[1]], [l[1], l[2]]), $"Line {l[1]} is in more than one part."),
            _ => (ApiClient.SplitBody([l[0]], [l[1]]), "Every line of the order must be in a part of the split."),
        };

        await AssertProblemAsync(await _client.SaveSplitAsync(orderId, body), HttpStatusCode.UnprocessableEntity, detail);
        await AssertProblemAsync(await _client.PreviewSplitAsync(orderId, body), HttpStatusCode.UnprocessableEntity, detail);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task A_rejected_put_keeps_the_saved_split()
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();

        var response = await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0], l[1]]));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal([[l[0]], [l[1]]], Parts(await GetSplitOkAsync(orderId)).Select(LineIds));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Put_and_preview_on_a_closed_order_are_422(string status)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        await _client.CloseOrderAsync(orderId, status);
        var body = ApiClient.SplitBody([l[0]], [l[1]]);
        var detail = $"The order is {status}; only an open order can be changed.";

        await AssertProblemAsync(await _client.SaveSplitAsync(orderId, body), HttpStatusCode.UnprocessableEntity, detail);
        await AssertProblemAsync(await _client.PreviewSplitAsync(orderId, body), HttpStatusCode.UnprocessableEntity, detail);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Delete_is_204_with_no_body_then_the_split_is_gone()
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();

        var response = await _client.RemoveSplitAsync(orderId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetSplitAsync(orderId)).StatusCode);
        await AssertProblemAsync(
            await _client.RemoveSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Delete_on_a_closed_order_with_a_split_is_422_and_keeps_it(string status)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();
        await _client.CloseOrderAsync(orderId, status);

        var response = await _client.RemoveSplitAsync(orderId);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity,
            $"The order is {status}; only an open order can be changed.");
        Assert.Equal(HttpStatusCode.OK, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Delete_on_a_paid_order_without_a_split_is_404()
    {
        var (orderId, _) = await OrderWithLinesAsync(1.00m, 2.00m);
        await _client.CloseOrderAsync(orderId, "Paid");

        await AssertProblemAsync(
            await _client.RemoveSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    public static TheoryData<string, bool> OrderChanges => new()
    {
        { "add-item", true },
        { "add-more-of-a-line", true },
        { "remove-line", true },
        { "change-quantity", true },
        { "same-quantity", false },
        { "apply-other-discount", true },
        { "apply-same-discount", false },
        { "remove-discount", true },
        { "set-other-tip", true },
        { "set-same-tip", false },
        { "remove-tip", true },
        { "set-other-payment-method", true },
        { "set-same-payment-method", false },
        { "pay", false },
        { "cancel", false },
    };

    /// <summary>
    /// Starts from an order with three lines, a 10% discount, a 15% tip and cash, and a saved split. Only a real change
    /// clears the split; a no-op, pay and cancel keep it.
    /// </summary>
    [Theory]
    [MemberData(nameof(OrderChanges))]
    public async Task Only_a_real_change_of_the_order_clears_the_saved_split(string change, bool clears)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m, 3.00m);
        (await _client.ApplyDiscountAsync(orderId, new { rate = 10 })).EnsureSuccessStatusCode();
        (await _client.SetTipAsync(orderId, 15)).EnsureSuccessStatusCode();
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1], l[2]]))).EnsureSuccessStatusCode();

        using var response = await SendChangeAsync(change, orderId, l);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(clears ? HttpStatusCode.NotFound : HttpStatusCode.OK, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    private async Task<HttpResponseMessage> SendChangeAsync(string change, Guid orderId, Guid[] l) => change switch
    {
        "add-item" => await _client.AddItemAsync(orderId, await _client.CreateMenuItemAsync("Tea", 2.00m), 1),
        "add-more-of-a-line" => await _client.AddItemAsync(orderId, await MenuItemOfLineAsync(orderId, l[0]), 1),
        "remove-line" => await _client.RemoveLineAsync(orderId, l[2]),
        "change-quantity" => await _client.ChangeLineQuantityAsync(orderId, l[0], 2),
        "same-quantity" => await _client.ChangeLineQuantityAsync(orderId, l[0], 1),
        "apply-other-discount" => await _client.ApplyDiscountAsync(orderId, new { rate = 20 }),
        "apply-same-discount" => await _client.ApplyDiscountAsync(orderId, new { rate = 10 }),
        "remove-discount" => await _client.RemoveDiscountAsync(orderId),
        "set-other-tip" => await _client.SetTipAsync(orderId, 18),
        "set-same-tip" => await _client.SetTipAsync(orderId, 15),
        "remove-tip" => await _client.RemoveTipAsync(orderId),
        "set-other-payment-method" => await _client.SetPaymentMethodAsync(orderId, "Card"),
        "set-same-payment-method" => await _client.SetPaymentMethodAsync(orderId, "Cash"),
        "pay" => await _client.PayOrderAsync(orderId),
        _ => await _client.CancelOrderAsync(orderId),
    };

    [Theory]
    [InlineData("remove-discount")]
    [InlineData("remove-tip")]
    public async Task Removing_an_adjustment_the_order_does_not_have_keeps_the_saved_split(string change)
    {
        var (orderId, l) = await OrderWithLinesAsync(1.00m, 2.00m);
        (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([l[0]], [l[1]]))).EnsureSuccessStatusCode();

        using var response = change == "remove-discount"
            ? await _client.RemoveDiscountAsync(orderId)
            : await _client.RemoveTipAsync(orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetSplitAsync(orderId)).StatusCode);
    }

    private async Task<Guid> MenuItemOfLineAsync(Guid orderId, Guid lineId) =>
        (await _client.GetOrderAsync(orderId)).GetProperty("lines").EnumerateArray()
        .Single(line => line.GetProperty("id").GetGuid() == lineId).GetProperty("menuItemId").GetGuid();

    private async Task<JsonElement> GetSplitOkAsync(Guid orderId)
    {
        var response = await _client.GetSplitAsync(orderId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    /// <summary>An order with one line per price, each from its own menu item; returns the line ids in that order.</summary>
    private async Task<(Guid OrderId, Guid[] LineIds)> OrderWithLinesAsync(params decimal[] prices)
    {
        var orderId = await _client.CreateOrderAsync();
        var lineIds = new List<Guid>();
        foreach (var price in prices)
        {
            var menuItemId = await _client.CreateMenuItemAsync($"Dish {lineIds.Count}", price);
            var response = await _client.AddItemAsync(orderId, menuItemId, 1);
            response.EnsureSuccessStatusCode();
            lineIds.Add((await response.ReadJsonAsync()).GetProperty("lines").EnumerateArray()
                .Single(line => line.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid());
        }

        return (orderId, [.. lineIds]);
    }

    private static JsonElement[] Parts(JsonElement split) => [.. split.GetProperty("parts").EnumerateArray()];

    private static Guid[] LineIds(JsonElement part) =>
        [.. part.GetProperty("lineIds").EnumerateArray().Select(id => id.GetGuid())];

    /// <summary>Line order inside a part is not stored, so parts are compared as sets of line ids.</summary>
    private static string LineSet(params Guid[] lineIds) =>
        string.Join(",", lineIds.Select(id => id.ToString()).Order(StringComparer.Ordinal));

    /// <summary>Same parts in the same order, same raw money strings; line ids compared as a set per part.</summary>
    private static void AssertSameSplit(JsonElement expected, JsonElement actual)
    {
        static string Describe(JsonElement part) =>
            LineSet(LineIds(part)) + " " + string.Join(" ", Amounts.Select(amount => part.Money(amount)));
        Assert.Equal(Parts(expected).Select(Describe), Parts(actual).Select(Describe));
    }

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(detail, (await response.ReadJsonAsync()).GetProperty("detail").GetString());
    }

    /// <summary>Adds up the returned part totals in the test only, to compare with the order total.</summary>
    private async Task AssertPartTotalsAddUpToGetTotalAsync(Guid orderId, JsonElement[] parts)
    {
        var order = await _client.GetOrderAsync(orderId);
        var sum = parts.Sum(p => decimal.Parse(p.Money("total"), CultureInfo.InvariantCulture));
        Assert.Equal(order.Money("total"), sum.ToString("0.00", CultureInfo.InvariantCulture));
    }
}
