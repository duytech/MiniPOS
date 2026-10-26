using Pos.Application.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems.CreateMenuItem;

public sealed class CreateMenuItemHandler(IMenuItemRepository menuItems, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateMenuItemCommand, MenuItemDto>
{
    public async Task<MenuItemDto> HandleAsync(CreateMenuItemCommand command, CancellationToken cancellationToken)
    {
        var menuItem = new MenuItem(command.Name.Trim(), command.Price);
        menuItems.Add(menuItem);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MenuItemDto.From(menuItem);
    }
}
