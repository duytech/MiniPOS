using Pos.Application.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public class MenuItemService(IMenuItemRepository menuItems, IUnitOfWork unitOfWork)
{
    public async Task<MenuItemDto> CreateAsync(CreateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var menuItem = new MenuItem(request.Name, request.Price, request.IsAvailable);
        menuItems.Add(menuItem);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return MenuItemDto.From(menuItem);
    }

    public async Task<MenuItemDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var menuItem = await menuItems.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException("Menu item", id);
        return MenuItemDto.From(menuItem);
    }

    public async Task<IReadOnlyList<MenuItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var items = await menuItems.ListAsync(cancellationToken);
        return items.Select(MenuItemDto.From).ToList();
    }
}
