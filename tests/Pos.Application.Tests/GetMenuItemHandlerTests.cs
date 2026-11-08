using Pos.Application.MenuItems.GetMenuItem;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.Tests;

public class GetMenuItemHandlerTests
{
    private readonly FakeMenuItemRepository _menuItems = new();
    private readonly GetMenuItemHandler _handler;

    public GetMenuItemHandlerTests() => _handler = new GetMenuItemHandler(_menuItems);

    [Fact]
    public async Task Returns_the_menu_item()
    {
        var menuItem = new MenuItem("Pho", Money.Usd(7.70m));
        _menuItems.Add(menuItem);

        var result = (await _handler.HandleAsync(new GetMenuItemQuery(menuItem.Id), CancellationToken.None)).Data;

        Assert.Equal(menuItem.Id, result.Id);
        Assert.Equal("Pho", result.Name);
        Assert.Equal(Money.Usd(7.70m), result.Price);
    }

    [Fact]
    public async Task Unknown_menu_item_is_not_found()
    {
        var result = await _handler.HandleAsync(new GetMenuItemQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorCode.MenuItemNotFound, result.Error.Code);
    }
}
