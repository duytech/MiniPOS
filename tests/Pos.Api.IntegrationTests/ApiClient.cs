using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Pos.Api.IntegrationTests;

/// <summary>Small helpers over the HTTP API. Responses are read as raw JSON so money formatting is checked too.</summary>
internal static class ApiClient
{
    public static async Task<Guid> CreateMenuItemAsync(this HttpClient client, string name, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/menu-items", new { name, price });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateOrderAsync(this HttpClient client)
    {
        var response = await client.PostAsync("/api/orders", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }

    public static Task<HttpResponseMessage> AddItemAsync(
        this HttpClient client, Guid orderId, Guid menuItemId, int quantity, string? notes = null) =>
        client.PostAsJsonAsync($"/api/orders/{orderId}/items", new { menuItemId, quantity, notes });

    public static Task<HttpResponseMessage> RemoveLineAsync(this HttpClient client, Guid orderId, Guid lineId) =>
        client.DeleteAsync($"/api/orders/{orderId}/lines/{lineId}");

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

    public static async Task<JsonElement> GetOrderAsync(this HttpClient client, Guid orderId)
    {
        var response = await client.GetAsync($"/api/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadJsonAsync();
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>The money value exactly as it appears in the JSON, e.g. <c>15.40</c>.</summary>
    public static string Money(this JsonElement element, string property) => element.GetProperty(property).GetRawText();
}
