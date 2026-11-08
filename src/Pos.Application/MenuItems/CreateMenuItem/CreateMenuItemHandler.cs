using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems.CreateMenuItem;

public sealed class CreateMenuItemHandler(IMenuItemRepository menuItems)
    : ICommandHandler<CreateMenuItemCommand, MenuItemDto>
{
    public async Task<Result<MenuItemDto>> HandleAsync(CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var menuItem = new MenuItem { Id = Guid.NewGuid(), Name = command.Name.Trim(), Price = command.Price };
        var saved = await menuItems.CreateAsync(menuItem, cancellationToken);
        return saved.IsSuccess ? Result.Ok(MenuItemDto.From(menuItem)) : Result.Fail<MenuItemDto>(saved.Error);
    }
}
