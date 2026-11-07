using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Application.MenuItems.GetMenuItem;

public sealed class GetMenuItemHandler(IMenuItemRepository menuItems) : IQueryHandler<GetMenuItemQuery, MenuItemDto>
{
    public async Task<Result<MenuItemDto>> HandleAsync(GetMenuItemQuery query, CancellationToken cancellationToken)
    {
        var menuItem = await menuItems.GetByIdAsync(query.Id, cancellationToken);
        if (menuItem is null)
        {
            return Result.Fail<MenuItemDto>(NotFoundErrors.MenuItem(query.Id));
        }

        return Result.Ok(MenuItemDto.From(menuItem));
    }
}
