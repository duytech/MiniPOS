using Pos.Domain.Common;

namespace Pos.Api.IntegrationTests;

public class MenuItemsApiTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(2.5, "2.50")]
    [InlineData(15.4, "15.40")]
    [InlineData(7.25, "7.25")]
    public async Task Known_id_returns_the_price_at_exactly_two_decimals(decimal price, string expected)
    {
        var id = await _client.CreateMenuItemAsync("Pho", price);

        var response = await _client.GetAsync($"/api/menu-items/{id}");

        await response.AssertSuccessAsync();
        var menuItem = await response.ReadDataAsync();
        Assert.Equal(id, menuItem.GetProperty("id").GetGuid());
        Assert.Equal("Pho", menuItem.GetProperty("name").GetString());
        Assert.Equal(expected, menuItem.Money("price"));
    }

    [Fact]
    public async Task Unknown_id_is_not_found()
    {
        var response = await _client.GetAsync($"/api/menu-items/{Guid.NewGuid()}");

        await response.AssertErrorAsync(ErrorCode.MenuItemNotFound);
    }
}
