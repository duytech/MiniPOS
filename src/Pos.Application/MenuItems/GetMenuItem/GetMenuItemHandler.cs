using Pos.Application.Common;

namespace Pos.Application.MenuItems.GetMenuItem;

public sealed class GetMenuItemHandler(IMenuItemRepository menuItems) : IQueryHandler<GetMenuItemQuery, MenuItemDto>
{
    public async Task<MenuItemDto> HandleAsync(GetMenuItemQuery query, CancellationToken cancellationToken)
    {
        var menuItem = await menuItems.GetByIdAsync(query.Id, cancellationToken);
        return MenuItemDto.From(menuItem!);
    }
}
