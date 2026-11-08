using Microsoft.Extensions.Logging;
using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems.CreateMenuItem;

public sealed class CreateMenuItemHandler(IMenuItemRepository menuItems, ILogger<CreateMenuItemHandler> logger)
    : ICommandHandler<CreateMenuItemCommand, MenuItemDto>
{
    public async Task<Result<MenuItemDto>> HandleAsync(CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var menuItem = new MenuItem { Id = Guid.NewGuid(), Name = command.Name.Trim(), Price = command.Price };
        try
        {
            await menuItems.CreateAsync(menuItem, cancellationToken);
        }
        catch (SaveFailedException ex)
        {
            logger.MenuItemSaveFailed(ex, menuItem.Id, ex.Code);
            return Result.Fail<MenuItemDto>(ex.ToError());
        }

        return Result.Ok(MenuItemDto.From(menuItem));
    }
}
