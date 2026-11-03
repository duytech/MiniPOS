using System.Net;

namespace Pos.Api.IntegrationTests;

public class MenuItemsApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(2.5, "2.50")]
    [InlineData(15.4, "15.40")]
    [InlineData(7.25, "7.25")]
    public async Task Known_id_returns_200_with_the_price_at_exactly_two_decimals(decimal price, string expected)
    {
        var id = await _client.CreateMenuItemAsync("Pho", price);

        var response = await _client.GetAsync($"/api/menu-items/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var menuItem = await response.ReadJsonAsync();
        Assert.Equal(id, menuItem.GetProperty("id").GetGuid());
        Assert.Equal("Pho", menuItem.GetProperty("name").GetString());
        Assert.Equal(expected, menuItem.Money("price"));
    }

    [Fact]
    public async Task Unknown_id_returns_404_problem_details()
    {
        var response = await _client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(404, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
    }
}
