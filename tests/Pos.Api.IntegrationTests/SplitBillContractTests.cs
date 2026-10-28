using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// The split bill contract of plans/tuan-3-ngay-7-split-bill.md, row by row over HTTP, read as raw JSON:
/// <c>GET|PUT|DELETE /api/orders/{id}/split</c> and <c>POST /api/orders/{id}/split/preview</c>.
/// Pricing config: tax 8.875%, card surcharge 3%. R1: each of discount, surcharge, tax and tip is shared by subtotal
/// share, rounded down to the cent; the leftover cents go to the largest part (the earlier one on a tie).
/// </summary>
public partial class SplitBillContractTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const int ConcurrentRequests = 8;
    private const string EmptyGuid = "00000000-0000-0000-0000-000000000000";
    private const string ConflictDetail = "The data was changed by another request. Reload and try again.";

    private static readonly string[] PartProperties = ["lineIds", "subtotal", "discount", "surcharge", "tax", "tip", "total"];
    private static readonly string[] MoneyProperties = ["subtotal", "discount", "surcharge", "tax", "tip", "total"];

    private readonly HttpClient _client = factory.CreateClient();

    // ---------------------------------------------------------------- money (R1, contract example)

    [Fact]
    public async Task Contract_example_60_and_40_gives_tax_5_33_and_3_55()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        // Order tax 8.875% of 100.00 = 8.875 -> 8.88. Shares 5.328 -> 5.32 and 3.552 -> 3.55, leftover 0.01 to 60.00.

        var response = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "60.00", "40.00");
        AssertMoney(parts, "discount", "0.00", "0.00");
        AssertMoney(parts, "surcharge", "0.00", "0.00");
        AssertMoney(parts, "tax", "5.33", "3.55");
        AssertMoney(parts, "tip", "0.00", "0.00");
        AssertMoney(parts, "total", "65.33", "43.55");
        AssertLineIds(parts[0], lines[0].Id);
        AssertLineIds(parts[1], lines[1].Id);
        await AssertPartTotalsAddUpToOrderTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task Leftover_cent_goes_to_the_largest_part_even_when_it_is_not_first()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);

        var response = await PreviewSplitAsync(orderId, [lines[1].Id], [lines[0].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "40.00", "60.00");
        AssertMoney(parts, "tax", "3.55", "5.33");
        AssertMoney(parts, "total", "43.55", "65.33");
        AssertLineIds(parts[0], lines[1].Id);
        AssertLineIds(parts[1], lines[0].Id);
    }

    [Fact]
    public async Task Discount_card_surcharge_and_tip_are_each_shared_by_subtotal_share()
    {
        var (orderId, lines) = await OrderWithLinesAsync(6.00m, 4.00m);
        (await _client.ApplyDiscountAsync(orderId, new { rate = 15 })).EnsureSuccessStatusCode();
        (await _client.SetPaymentMethodAsync(orderId, "Card")).EnsureSuccessStatusCode();
        (await _client.SetTipAsync(orderId, 18m)).EnsureSuccessStatusCode();
        // Order: subtotal 10.00, discount 1.50, surcharge 0.26, tax 0.78, tip 1.53, total 11.07. Shares 60% / 40%:
        // discount 0.90 / 0.60; surcharge 0.156 -> 0.15, 0.104 -> 0.10, +0.01 to the 6.00 part;
        // tax 0.468 -> 0.46, 0.312 -> 0.31, +0.01; tip 0.918 -> 0.91, 0.612 -> 0.61, +0.01.

        var response = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "6.00", "4.00");
        AssertMoney(parts, "discount", "0.90", "0.60");
        AssertMoney(parts, "surcharge", "0.16", "0.10");
        AssertMoney(parts, "tax", "0.47", "0.31");
        AssertMoney(parts, "tip", "0.92", "0.61");
        AssertMoney(parts, "total", "6.65", "4.42");
        await AssertPartTotalsAddUpToOrderTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task On_a_tie_for_largest_the_earlier_part_gets_the_leftover_cent()
    {
        var (orderId, lines) = await OrderWithLinesAsync(5.00m, 5.00m, 2.50m);
        // Order tax 8.875% of 12.50 = 1.109375 -> 1.11. Shares 40% / 40% / 20%: 0.444 -> 0.44, 0.44, 0.222 -> 0.22.
        // Leftover 0.01; parts 1 and 2 tie for largest, the earlier (part 1) gets it.

        var response = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id], [lines[2].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "5.00", "5.00", "2.50");
        AssertMoney(parts, "tax", "0.45", "0.44", "0.22");
        AssertMoney(parts, "total", "5.45", "5.44", "2.72");
        await AssertPartTotalsAddUpToOrderTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task All_leftover_cents_go_to_the_largest_part()
    {
        var (orderId, lines) = await OrderWithLinesAsync(3.33m, 3.33m, 3.33m);
        // Order tax 8.875% of 9.99 = 0.8866125 -> 0.89. Each third 0.29666 -> 0.29, sum 0.87, leftover 0.02.
        // All three tie, so the first part gets both cents: 0.31 / 0.29 / 0.29.

        var response = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id], [lines[2].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "3.33", "3.33", "3.33");
        AssertMoney(parts, "tax", "0.31", "0.29", "0.29");
        AssertMoney(parts, "total", "3.64", "3.62", "3.62");
        await AssertPartTotalsAddUpToOrderTotalAsync(orderId, parts);
    }

    [Fact]
    public async Task A_part_with_several_lines_has_their_summed_subtotal()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 25.00m, 15.00m);
        // Subtotal 100.00, so the same shares as the contract example: tax 5.33 / 3.55.

        var response = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id, lines[2].Id]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var parts = AssertSplitShape(await response.ReadJsonAsync());
        AssertMoney(parts, "subtotal", "60.00", "40.00");
        AssertMoney(parts, "tax", "5.33", "3.55");
        AssertMoney(parts, "total", "65.33", "43.55");
        AssertLineIds(parts[1], lines[1].Id, lines[2].Id);
    }

    // ---------------------------------------------------------------- GET /split

    [Fact]
    public async Task Get_returns_the_saved_split_exactly_as_the_put_returned_it()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var saved = await (await PutSplitAsync(orderId, [lines[1].Id], [lines[0].Id])).ReadJsonAsync();

        var response = await GetSplitAsync(orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var got = await response.ReadJsonAsync();
        AssertSplitShape(got);
        Assert.Equal(Normalize(saved), Normalize(got));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Get_still_returns_the_saved_split_on_a_closed_order(string status)
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var saved = await (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).ReadJsonAsync();
        await _client.CloseOrderAsync(orderId, status);

        var response = await GetSplitAsync(orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Normalize(saved), Normalize(await response.ReadJsonAsync()));
    }

    [Fact]
    public async Task Get_with_no_saved_split_is_404_split_not_found()
    {
        var (orderId, _) = await OrderWithLinesAsync(60.00m, 40.00m);

        await AssertProblemAsync(await GetSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Get_on_a_closed_order_that_never_had_a_split_is_404(string status)
    {
        var (orderId, _) = await OrderWithLinesAsync(60.00m, 40.00m);
        await _client.CloseOrderAsync(orderId, status);

        await AssertProblemAsync(await GetSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Fact]
    public async Task Get_on_an_unknown_order_is_404_order_not_found()
    {
        var orderId = Guid.NewGuid();

        await AssertProblemAsync(await GetSplitAsync(orderId), HttpStatusCode.NotFound, $"Order '{orderId}' was not found.");
    }

    [Fact]
    public async Task Get_with_an_empty_guid_order_id_is_400()
    {
        var response = await _client.GetAsync($"/api/orders/{EmptyGuid}/split");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------- PUT /split

    [Fact]
    public async Task Put_replaces_the_saved_split()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 25.00m, 15.00m);
        (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id, lines[2].Id])).EnsureSuccessStatusCode();

        var second = await PutSplitAsync(orderId, [lines[2].Id], [lines[1].Id], [lines[0].Id]);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.ReadJsonAsync();
        var got = await (await GetSplitAsync(orderId)).ReadJsonAsync();
        Assert.Equal(Normalize(secondBody), Normalize(got));
        var parts = AssertSplitShape(got);
        AssertMoney(parts, "subtotal", "15.00", "25.00", "60.00");
        AssertLineIds(parts[0], lines[2].Id);
        AssertLineIds(parts[1], lines[1].Id);
        AssertLineIds(parts[2], lines[0].Id);
    }

    [Fact]
    public async Task Put_on_an_unknown_order_is_404()
    {
        var orderId = Guid.NewGuid();

        var response = await PutSplitAsync(orderId, [Guid.NewGuid()], [Guid.NewGuid()]);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Order '{orderId}' was not found.");
    }

    public static TheoryData<string, string> BadBodies => new()
    {
        { "put", "" },
        { "put", "{}" },
        { "put", """{ "parts": null }""" },
        { "put", """{ "parts": [ { "lineIds": null }, { "lineIds": ["{b}"] } ] }""" },
        { "put", """{ "parts": [ {}, { "lineIds": ["{b}"] } ] }""" },
        { "put", $$"""{ "parts": [ { "lineIds": ["{a}", "{{EmptyGuid}}"] }, { "lineIds": ["{b}"] } ] }""" },
        { "preview", "" },
        { "preview", "{}" },
        { "preview", """{ "parts": null }""" },
        { "preview", """{ "parts": [ { "lineIds": null }, { "lineIds": ["{b}"] } ] }""" },
        { "preview", """{ "parts": [ {}, { "lineIds": ["{b}"] } ] }""" },
        { "preview", $$"""{ "parts": [ { "lineIds": ["{a}", "{{EmptyGuid}}"] }, { "lineIds": ["{b}"] } ] }""" },
    };

    [Theory]
    [MemberData(nameof(BadBodies))]
    public async Task Body_missing_parts_null_line_ids_null_or_empty_guid_line_is_400_and_stores_nothing(
        string route, string template)
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var body = template.Replace("{a}", lines[0].Id.ToString(), StringComparison.Ordinal)
            .Replace("{b}", lines[1].Id.ToString(), StringComparison.Ordinal);

        var response = await SendRawAsync(route, orderId, body);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.NotFound, (await GetSplitAsync(orderId)).StatusCode);
    }

    public static TheoryData<string, string> DomainCases => new()
    {
        { "put", "no-parts" },
        { "put", "one-part" },
        { "put", "empty-part" },
        { "put", "unknown-line" },
        { "put", "line-of-another-order" },
        { "put", "line-in-two-parts" },
        { "put", "line-left-out" },
        { "preview", "no-parts" },
        { "preview", "one-part" },
        { "preview", "empty-part" },
        { "preview", "unknown-line" },
        { "preview", "line-of-another-order" },
        { "preview", "line-in-two-parts" },
        { "preview", "line-left-out" },
    };

    [Theory]
    [MemberData(nameof(DomainCases))]
    public async Task Broken_split_rule_is_422_with_the_contract_detail_and_stores_nothing(string route, string rule)
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 25.00m, 15.00m);
        var (a, b, c) = (lines[0].Id, lines[1].Id, lines[2].Id);
        var stranger = Guid.NewGuid();
        var (otherOrderId, otherLines) = await OrderWithLinesAsync(1.00m, 2.00m);
        Assert.NotEqual(orderId, otherOrderId);
        var foreign = otherLines[0].Id;

        var (parts, detail) = rule switch
        {
            "no-parts" => (Parts(), "A split needs at least two parts."),
            "one-part" => (Parts([a, b, c]), "A split needs at least two parts."),
            "empty-part" => (Parts([a, b, c], []), "Every part of a split needs at least one line."),
            "unknown-line" => (Parts([a, b], [c, stranger]), $"Line {stranger} is not on this order."),
            "line-of-another-order" => (Parts([a, b], [c, foreign]), $"Line {foreign} is not on this order."),
            "line-in-two-parts" => (Parts([a, b], [b, c]), $"Line {b} is in more than one part."),
            "line-left-out" => (Parts([a], [b]), "Every line of the order must be in a part of the split."),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
        };

        var response = await SendSplitAsync(route, orderId, parts);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, detail);
        Assert.Equal(HttpStatusCode.NotFound, (await GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Rejected_put_keeps_the_previously_saved_split()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var saved = await (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).ReadJsonAsync();

        var response = await PutSplitAsync(orderId, [lines[0].Id, lines[1].Id]);

        await AssertProblemAsync(response, HttpStatusCode.UnprocessableEntity, "A split needs at least two parts.");
        Assert.Equal(Normalize(saved), Normalize(await (await GetSplitAsync(orderId)).ReadJsonAsync()));
    }

    [Theory]
    [InlineData("put", "Paid")]
    [InlineData("put", "Cancelled")]
    [InlineData("preview", "Paid")]
    [InlineData("preview", "Cancelled")]
    public async Task Put_or_preview_on_a_closed_order_is_422_not_open(string route, string status)
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        await _client.CloseOrderAsync(orderId, status);

        var response = await SendSplitAsync(route, orderId, [[lines[0].Id], [lines[1].Id]]);

        await AssertProblemAsync(
            response, HttpStatusCode.UnprocessableEntity, $"The order is {status}; only an open order can be changed.");
        Assert.Equal(HttpStatusCode.NotFound, (await GetSplitAsync(orderId)).StatusCode);
    }

    [Fact]
    public async Task Parallel_puts_are_200_or_409_and_the_stored_split_is_one_accepted_body()
    {
        var (orderId, lines) = await OrderWithLinesAsync(1.10m, 2.20m, 3.30m, 4.40m);
        var (a, b, c, d) = (lines[0].Id, lines[1].Id, lines[2].Id, lines[3].Id);
        Guid[][][] groupings =
        [
            [[a], [b, c, d]],
            [[b], [a, c, d]],
            [[c], [a, b, d]],
            [[d], [a, b, c]],
            [[a, b], [c, d]],
            [[a, c], [b, d]],
            [[a, d], [b, c]],
            [[a], [b], [c], [d]],
        ];
        Assert.Equal(ConcurrentRequests, groupings.Length);

        var results = await ConcurrentlyAsync(groupings.Select(g => (Func<Task<HttpResponseMessage>>)(() =>
            PutSplitAsync(orderId, g))));

        var unexpected = results.Where(r => r.Status is not (HttpStatusCode.OK or HttpStatusCode.Conflict)).ToList();
        Assert.True(unexpected.Count == 0, $"Expected only 200 or 409, got: {Describe(unexpected)}");
        Assert.Contains(results, r => r.Status == HttpStatusCode.Conflict); // the requests really raced
        foreach (var conflict in results.Where(r => r.Status == HttpStatusCode.Conflict))
        {
            Assert.Equal("application/problem+json", conflict.MediaType);
            Assert.Equal(ConflictDetail, conflict.Body.GetProperty("detail").GetString());
        }

        var accepted = results.Where(r => r.Status == HttpStatusCode.OK).Select(r => Normalize(r.Body)).ToList();
        Assert.NotEmpty(accepted);
        var stored = await GetSplitAsync(orderId);
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.Contains(Normalize(await stored.ReadJsonAsync()), accepted);
    }

    // ---------------------------------------------------------------- POST /split/preview

    [Fact]
    public async Task Preview_returns_the_same_amounts_as_put_and_stores_nothing()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);

        var preview = await PreviewSplitAsync(orderId, [lines[0].Id], [lines[1].Id]);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.ReadJsonAsync();
        var parts = AssertSplitShape(previewBody);
        AssertMoney(parts, "tax", "5.33", "3.55");
        AssertMoney(parts, "total", "65.33", "43.55");
        await AssertProblemAsync(await GetSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");

        var saved = await (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).ReadJsonAsync();
        Assert.Equal(Normalize(previewBody), Normalize(saved));
    }

    [Fact]
    public async Task Preview_does_not_replace_a_saved_split()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var saved = await (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).ReadJsonAsync();

        var preview = await PreviewSplitAsync(orderId, [lines[1].Id], [lines[0].Id]);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(Normalize(saved), Normalize(await (await GetSplitAsync(orderId)).ReadJsonAsync()));
    }

    [Fact]
    public async Task Preview_on_an_unknown_order_is_404()
    {
        var orderId = Guid.NewGuid();

        var response = await PreviewSplitAsync(orderId, [Guid.NewGuid()], [Guid.NewGuid()]);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, $"Order '{orderId}' was not found.");
    }

    // ---------------------------------------------------------------- DELETE /split

    [Fact]
    public async Task Delete_is_204_with_no_body_then_get_and_delete_are_404()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).EnsureSuccessStatusCode();

        var response = await DeleteSplitAsync(orderId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        await AssertProblemAsync(await GetSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
        await AssertProblemAsync(await DeleteSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Fact]
    public async Task Delete_with_no_saved_split_is_404()
    {
        var (orderId, _) = await OrderWithLinesAsync(60.00m, 40.00m);

        await AssertProblemAsync(await DeleteSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Fact]
    public async Task Delete_on_an_unknown_order_is_404_order_not_found()
    {
        var orderId = Guid.NewGuid();

        await AssertProblemAsync(await DeleteSplitAsync(orderId), HttpStatusCode.NotFound, $"Order '{orderId}' was not found.");
    }

    [Fact]
    public async Task Delete_with_an_empty_guid_order_id_is_400()
    {
        var response = await _client.DeleteAsync($"/api/orders/{EmptyGuid}/split");

        await AssertProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Delete_on_a_closed_order_with_a_split_is_422_and_keeps_it(string status)
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        var saved = await (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).ReadJsonAsync();
        await _client.CloseOrderAsync(orderId, status);

        var response = await DeleteSplitAsync(orderId);

        await AssertProblemAsync(
            response, HttpStatusCode.UnprocessableEntity, $"The order is {status}; only an open order can be changed.");
        Assert.Equal(Normalize(saved), Normalize(await (await GetSplitAsync(orderId)).ReadJsonAsync()));
    }

    [Theory]
    [InlineData("Paid")]
    [InlineData("Cancelled")]
    public async Task Delete_on_a_closed_order_with_no_split_is_404_before_422(string status)
    {
        var (orderId, _) = await OrderWithLinesAsync(60.00m, 40.00m);
        await _client.CloseOrderAsync(orderId, status);

        await AssertProblemAsync(await DeleteSplitAsync(orderId), HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
    }

    [Fact]
    public async Task Parallel_deletes_remove_the_split_once()
    {
        var (orderId, lines) = await OrderWithLinesAsync(60.00m, 40.00m);
        (await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id])).EnsureSuccessStatusCode();

        var results = await ConcurrentlyAsync(Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => (Func<Task<HttpResponseMessage>>)(() => DeleteSplitAsync(orderId))));

        var unexpected = results
            .Where(r => r.Status is not (HttpStatusCode.NoContent or HttpStatusCode.NotFound or HttpStatusCode.Conflict))
            .ToList();
        Assert.True(unexpected.Count == 0, $"Expected only 204, 404 or 409, got: {Describe(unexpected)}");
        Assert.Single(results, r => r.Status == HttpStatusCode.NoContent);
        Assert.Equal(HttpStatusCode.NotFound, (await GetSplitAsync(orderId)).StatusCode);
    }

    // ---------------------------------------------------------------- R5: real change clears, no-op keeps

    public static TheoryData<string, bool> OrderChanges => new()
    {
        { "add-new-item", true },
        { "add-identical-item", true },
        { "remove-line", true },
        { "quantity-different", true },
        { "quantity-same", false },
        { "discount-new", true },
        { "discount-different-rate", true },
        { "discount-same-rate", false },
        { "discount-same-amount", false },
        { "discount-remove-existing", true },
        { "discount-remove-none", false },
        { "tip-new", true },
        { "tip-different", true },
        { "tip-same", false },
        { "tip-remove-existing", true },
        { "tip-remove-none", false },
        { "payment-method-different", true },
        { "payment-method-same", false },
        { "pay", false },
        { "cancel", false },
    };

    [Theory]
    [MemberData(nameof(OrderChanges))]
    public async Task Only_a_real_change_clears_the_saved_split(string change, bool clears)
    {
        var (orderId, lines) = await OrderWithLinesAsync(6.00m, 4.00m, 2.00m);
        await PrepareAsync(orderId, change);
        var saved = await PutSplitAsync(orderId, [lines[0].Id], [lines[1].Id, lines[2].Id]);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedBody = await saved.ReadJsonAsync();

        var response = await ChangeAsync(orderId, lines, change);

        Assert.True(response.IsSuccessStatusCode, $"{change}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var split = await GetSplitAsync(orderId);
        if (clears)
        {
            await AssertProblemAsync(split, HttpStatusCode.NotFound, $"Split '{orderId}' was not found.");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, split.StatusCode);
            Assert.Equal(Normalize(savedBody), Normalize(await split.ReadJsonAsync()));
        }
    }

    private async Task PrepareAsync(Guid orderId, string change)
    {
        HttpResponseMessage? response = change switch
        {
            "discount-different-rate" or "discount-same-rate" or "discount-remove-existing" =>
                await _client.ApplyDiscountAsync(orderId, new { rate = 10 }),
            "discount-same-amount" => await _client.ApplyDiscountAsync(orderId, new { amount = 2.50m }),
            "tip-different" or "tip-same" or "tip-remove-existing" => await _client.SetTipAsync(orderId, 15m),
            _ => null,
        };
        response?.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> ChangeAsync(Guid orderId, IReadOnlyList<OrderLineRef> lines, string change)
    {
        switch (change)
        {
            case "add-new-item":
                return await _client.AddItemAsync(orderId, await _client.CreateMenuItemAsync("Tea", 1.50m), 1);
            case "add-identical-item":
                return await _client.AddItemAsync(orderId, lines[0].MenuItemId, 1);
            case "remove-line":
                return await _client.RemoveLineAsync(orderId, lines[2].Id);
            case "quantity-different":
                return await _client.ChangeLineQuantityAsync(orderId, lines[0].Id, 2);
            case "quantity-same":
                return await _client.ChangeLineQuantityAsync(orderId, lines[0].Id, 1);
            case "discount-new" or "discount-different-rate":
                return await _client.ApplyDiscountAsync(orderId, new { rate = 15 });
            case "discount-same-rate":
                return await _client.ApplyDiscountAsync(orderId, new { rate = 10 });
            case "discount-same-amount":
                return await _client.ApplyDiscountAsync(orderId, new { amount = 2.50m });
            case "discount-remove-existing" or "discount-remove-none":
                return await _client.RemoveDiscountAsync(orderId);
            case "tip-new" or "tip-different":
                return await _client.SetTipAsync(orderId, 18m);
            case "tip-same":
                return await _client.SetTipAsync(orderId, 15m);
            case "tip-remove-existing" or "tip-remove-none":
                return await _client.RemoveTipAsync(orderId);
            case "payment-method-different":
            case "payment-method-same":
                var current = (await _client.GetOrderAsync(orderId)).GetProperty("paymentMethod").GetString();
                var other = current == "Card" ? "Cash" : "Card";
                return await _client.SetPaymentMethodAsync(orderId, change == "payment-method-same" ? current! : other);
            case "pay":
                return await _client.PayOrderAsync(orderId);
            case "cancel":
                return await _client.CancelOrderAsync(orderId);
            default:
                throw new ArgumentOutOfRangeException(nameof(change), change, null);
        }
    }

    // ---------------------------------------------------------------- private HTTP helpers

    private static Guid[][] Parts(params Guid[][] parts) => parts;

    private sealed record OrderLineRef(Guid Id, Guid MenuItemId);

    private sealed record Result(HttpStatusCode Status, string? MediaType, JsonElement Body);

    private Task<HttpResponseMessage> GetSplitAsync(Guid orderId) => _client.GetAsync($"/api/orders/{orderId}/split");

    private Task<HttpResponseMessage> DeleteSplitAsync(Guid orderId) => _client.DeleteAsync($"/api/orders/{orderId}/split");

    private Task<HttpResponseMessage> PutSplitAsync(Guid orderId, params Guid[][] parts) => SendSplitAsync("put", orderId, parts);

    private Task<HttpResponseMessage> PreviewSplitAsync(Guid orderId, params Guid[][] parts) =>
        SendSplitAsync("preview", orderId, parts);

    private Task<HttpResponseMessage> SendSplitAsync(string route, Guid orderId, Guid[][] parts)
    {
        var body = new { parts = parts.Select(p => new { lineIds = p }).ToArray() };
        return route == "put"
            ? _client.PutAsJsonAsync($"/api/orders/{orderId}/split", body)
            : _client.PostAsJsonAsync($"/api/orders/{orderId}/split/preview", body);
    }

    private Task<HttpResponseMessage> SendRawAsync(string route, Guid orderId, string json)
    {
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        return route == "put"
            ? _client.PutAsync($"/api/orders/{orderId}/split", content)
            : _client.PostAsync($"/api/orders/{orderId}/split/preview", content);
    }

    /// <summary>An open order with one line per price, each from its own menu item; lines are returned in that order.</summary>
    private async Task<(Guid OrderId, OrderLineRef[] Lines)> OrderWithLinesAsync(params decimal[] prices)
    {
        var orderId = await _client.CreateOrderAsync();
        var lines = new List<OrderLineRef>();
        for (var i = 0; i < prices.Length; i++)
        {
            var menuItemId = await _client.CreateMenuItemAsync(
                $"Split item {i} {Guid.NewGuid():N}", prices[i]);
            var response = await _client.AddItemAsync(orderId, menuItemId, 1);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var lineId = (await response.ReadJsonAsync()).GetProperty("lines").EnumerateArray()
                .Single(l => l.GetProperty("menuItemId").GetGuid() == menuItemId).GetProperty("id").GetGuid();
            lines.Add(new OrderLineRef(lineId, menuItemId));
        }

        return (orderId, [.. lines]);
    }

    private static async Task<IReadOnlyList<Result>> ConcurrentlyAsync(IEnumerable<Func<Task<HttpResponseMessage>>> requests)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = requests
            .Select(async send =>
            {
                await start.Task;
                using var response = await send();
                var text = await response.Content.ReadAsStringAsync();
                var body = text.Length == 0 ? default : await response.ReadJsonAsync();
                return new Result(response.StatusCode, response.Content.Headers.ContentType?.MediaType, body);
            })
            .ToList();

        start.SetResult();
        return await Task.WhenAll(running);
    }

    private static string Describe(IEnumerable<Result> results) =>
        string.Join(", ", results.GroupBy(r => r.Status).Select(g => $"{(int)g.Key} x{g.Count()}"));

    // ---------------------------------------------------------------- assertions

    /// <summary>Checks <c>{ "parts": [ SplitPartDto... ] }</c> with exact property names and 2-decimal money numbers.</summary>
    private static JsonElement[] AssertSplitShape(JsonElement split)
    {
        Assert.Equal(JsonValueKind.Object, split.ValueKind);
        Assert.Equal(["parts"], split.EnumerateObject().Select(p => p.Name));
        var parts = split.GetProperty("parts").EnumerateArray().ToArray();
        foreach (var part in parts)
        {
            Assert.Equal(PartProperties.Order(StringComparer.Ordinal),
                part.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            Assert.Equal(JsonValueKind.Array, part.GetProperty("lineIds").ValueKind);
            foreach (var money in MoneyProperties)
            {
                var value = part.GetProperty(money);
                Assert.Equal(JsonValueKind.Number, value.ValueKind);
                Assert.Matches(TwoDecimals(), value.GetRawText());
            }
        }

        return parts;
    }

    private static void AssertMoney(JsonElement[] parts, string property, params string[] expected) =>
        Assert.Equal(expected, parts.Select(p => p.Money(property)));

    /// <summary>The order of ids inside a part is not in the contract, so they are compared as a set.</summary>
    private static void AssertLineIds(JsonElement part, params Guid[] expected) =>
        Assert.Equal(
            expected.Order(),
            part.GetProperty("lineIds").EnumerateArray().Select(id => id.GetGuid()).Order());

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? detail = null)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {(int)status}, got {(int)response.StatusCode}: {text}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        if (detail is not null)
        {
            using var document = JsonDocument.Parse(text);
            Assert.Equal(detail, document.RootElement.GetProperty("detail").GetString());
        }
    }

    /// <summary>Adds up the returned part totals in the test only, to compare with GET /api/orders/{id}.</summary>
    private async Task AssertPartTotalsAddUpToOrderTotalAsync(Guid orderId, JsonElement[] parts)
    {
        var order = await _client.GetOrderAsync(orderId);
        var sum = parts.Sum(p => decimal.Parse(p.Money("total"), CultureInfo.InvariantCulture));
        Assert.Equal(order.Money("total"), sum.ToString("0.00", CultureInfo.InvariantCulture));
    }

    /// <summary>Re-serializes the element so two bodies compare by content; numbers keep their raw text.</summary>
    private static string Normalize(JsonElement element) => JsonSerializer.Serialize(element);

    [GeneratedRegex(@"^-?\d+\.\d{2}$")]
    private static partial Regex TwoDecimals();
}
