using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>Pins the whole body of each kind of answer: every expected outcome is HTTP 200 with the same envelope.</summary>
public class EnvelopeContractTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Success_has_the_data_and_no_location_header()
    {
        var response = await _client.PostAsync("/api/orders", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var body = await response.ReadJsonAsync();
        Assert.Equal(["isSuccess", "data", "error"], body.EnumerateObject().Select(p => p.Name));
        Assert.True(body.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(JsonValueKind.Object, body.GetProperty("data").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("error").ValueKind);
    }

    [Fact]
    public async Task Removing_a_split_answers_true()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var tea = await _client.CreateMenuItemAsync("Tea", 2.00m);
        var orderId = await _client.CreateOrderAsync();
        await (await _client.AddItemAsync(orderId, pho, 1)).AssertSuccessAsync();
        var order = await (await _client.AddItemAsync(orderId, tea, 1)).ReadDataAsync();
        var itemIds = order.GetProperty("items").EnumerateArray().Select(l => l.GetProperty("id").GetGuid()).ToArray();
        await (await _client.SaveSplitAsync(orderId, ApiClient.SplitBody([itemIds[0]], [itemIds[1]]))).AssertSuccessAsync();

        var response = await _client.RemoveSplitAsync(orderId);

        Assert.True((await response.ReadDataAsync()).GetBoolean());
    }

    [Fact]
    public async Task Failed_split_removal_has_null_data()
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.RemoveSplitAsync(orderId);

        Assert.Equal(
            $$$"""{"isSuccess":false,"data":null,"error":{"code":"SplitNotFound","message":"Split '{{{orderId}}}' was not found."}}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Broken_rule_has_the_code_and_message()
    {
        var orderId = await _client.CreateOrderAsync();
        await _client.CloseOrderAsync(orderId, "Paid");

        var response = await _client.PayOrderAsync(orderId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            """{"isSuccess":false,"data":null,"error":{"code":"OrderNotOpen","message":"The order is Paid; only an open order can be changed."}}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Failed_validator_has_its_messages_in_the_message()
    {
        var pho = await _client.CreateMenuItemAsync("Pho", 7.70m);
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.AddItemAsync(orderId, pho, 0);

        Assert.Equal(
            """{"isSuccess":false,"data":null,"error":{"code":"ValidationFailed","message":"'Quantity' must be greater than '0'."}}""",
            await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Malformed_json_is_rejected_before_the_handler_with_the_same_envelope()
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.PutAsync(
            $"/api/orders/{orderId}/tip", new StringContent("""{ "rate": """, Encoding.UTF8, "application/json"));

        await response.AssertErrorAsync(ErrorCode.ValidationFailed);
        var body = await response.ReadJsonAsync();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        Assert.Equal(["code", "message"], body.GetProperty("error").EnumerateObject().Select(p => p.Name));
        Assert.NotEmpty(body.GetProperty("error").GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task Missing_required_field_is_rejected_before_the_handler_with_the_same_envelope()
    {
        var orderId = await _client.CreateOrderAsync();

        var response = await _client.PutAsJsonAsync($"/api/orders/{orderId}/tip", new { });

        Assert.Equal(
            """{"isSuccess":false,"data":null,"error":{"code":"ValidationFailed","message":"The Rate field is required."}}""",
            await response.Content.ReadAsStringAsync());
    }
}
