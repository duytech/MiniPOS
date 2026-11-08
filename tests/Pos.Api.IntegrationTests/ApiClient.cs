using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

/// <summary>Small helpers over the HTTP API. Responses are read as raw JSON so money formatting is checked too.</summary>
internal static class ApiClient
{
    public static async Task<Guid> CreateMenuItemAsync(this HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new { name, price });
        return (await response.ReadDataAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateOrderAsync(this HttpClient client)
    {
        var response = await client.PostAsync("/api/orders", null);
        return (await response.ReadDataAsync()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> AddItemAsync(
        this HttpClient client, Guid orderId, Guid menuItemId, int quantity, string? notes = null) =>
        client.PostAsJsonAsync($"/api/orders/{orderId}/items", new { menuItemId, quantity, notes });

    public static Task<HttpResponseMessage> RemoveLineAsync(this HttpClient client, Guid orderId, Guid lineId) =>
        client.DeleteAsync($"/api/orders/{orderId}/lines/{lineId}");

    public static Task<HttpResponseMessage> ChangeLineQuantityAsync(
        this HttpClient client, Guid orderId, Guid lineId, int quantity) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/lines/{lineId}/quantity", new { quantity });

    /// <summary>Body is sent as given, e.g. <c>new { rate = 15 }</c> or <c>new { amount = 2.50 }</c>.</summary>
    public static Task<HttpResponseMessage> ApplyDiscountAsync(this HttpClient client, Guid orderId, object body) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/discount", body);

    public static Task<HttpResponseMessage> RemoveDiscountAsync(this HttpClient client, Guid orderId) =>
        client.DeleteAsync($"/api/orders/{orderId}/discount");

    public static Task<HttpResponseMessage> SetTipAsync(this HttpClient client, Guid orderId, decimal rate) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/tip", new { rate });

    public static Task<HttpResponseMessage> RemoveTipAsync(this HttpClient client, Guid orderId) =>
        client.DeleteAsync($"/api/orders/{orderId}/tip");

    public static Task<HttpResponseMessage> SetPaymentMethodAsync(this HttpClient client, Guid orderId, string paymentMethod) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/payment-method", new { paymentMethod });

    public static Task<HttpResponseMessage> PayOrderAsync(this HttpClient client, Guid orderId) =>
        client.PostAsync($"/api/orders/{orderId}/pay", null);

    public static Task<HttpResponseMessage> CancelOrderAsync(this HttpClient client, Guid orderId) =>
        client.PostAsync($"/api/orders/{orderId}/cancel", null);

    /// <summary>Closes the order through the API: <c>"Paid"</c> pays it, <c>"Cancelled"</c> cancels it.</summary>
    public static async Task CloseOrderAsync(this HttpClient client, Guid orderId, string status)
    {
        var response = status switch
        {
            "Paid" => await client.PayOrderAsync(orderId),
            "Cancelled" => await client.CancelOrderAsync(orderId),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Paid or Cancelled"),
        };
        await response.AssertSuccessAsync();
    }

    /// <summary>Body of PUT and preview split: one array of line ids per part, in part order.</summary>
    public static object SplitBody(params IEnumerable<Guid>[] parts) =>
        new { parts = parts.Select(lineIds => new { lineIds }).ToArray() };

    public static Task<HttpResponseMessage> GetSplitAsync(this HttpClient client, Guid orderId) =>
        client.GetAsync($"/api/orders/{orderId}/split");

    /// <summary>Body is sent as given; <see cref="SplitBody"/> builds a valid one.</summary>
    public static Task<HttpResponseMessage> SaveSplitAsync(this HttpClient client, Guid orderId, object body) =>
        client.PutAsJsonAsync($"/api/orders/{orderId}/split", body);

    /// <summary>Body is sent as given; <see cref="SplitBody"/> builds a valid one.</summary>
    public static Task<HttpResponseMessage> PreviewSplitAsync(this HttpClient client, Guid orderId, object body) =>
        client.PostAsJsonAsync($"/api/orders/{orderId}/split/preview", body);

    public static Task<HttpResponseMessage> RemoveSplitAsync(this HttpClient client, Guid orderId) =>
        client.DeleteAsync($"/api/orders/{orderId}/split");

    public static async Task<JsonElement> GetOrderAsync(this HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/orders/{orderId}");
        return await response.ReadDataAsync();
    }

    /// <summary>Null on success, else the error code. Every expected outcome is HTTP 200, so anything else fails here.</summary>
    public static async Task<ErrorCode?> OutcomeAsync(this HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Expected HTTP 200, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var body = await response.ReadJsonAsync();
        return body.GetProperty("isSuccess").GetBoolean()
            ? null
            : Enum.Parse<ErrorCode>(body.GetProperty("error").GetProperty("code").GetString()!);
    }

    public static async Task AssertSuccessAsync(this HttpResponseMessage response)
    {
        var outcome = await response.OutcomeAsync();
        Assert.True(outcome is null, $"Expected success, got: {await response.Content.ReadAsStringAsync()}");
    }

    public static async Task AssertErrorAsync(this HttpResponseMessage response, ErrorCode code, string? message = null)
    {
        Assert.Equal(code, await response.OutcomeAsync());
        if (message is not null)
        {
            Assert.Equal(message, (await response.ReadJsonAsync()).GetProperty("error").GetProperty("message").GetString());
        }
    }

    /// <summary>The <c>data</c> of a successful response; fails on anything else.</summary>
    public static async Task<JsonElement> ReadDataAsync(this HttpResponseMessage response)
    {
        await response.AssertSuccessAsync();
        return (await response.ReadJsonAsync()).GetProperty("data");
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>The money value exactly as it appears in the JSON, e.g. <c>15.40</c>.</summary>
    public static string Money(this JsonElement element, string property) => element.GetProperty(property).GetRawText();
}
