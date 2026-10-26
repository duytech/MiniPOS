using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public sealed record MenuItemDto(Guid Id, string Name, Money Price)
{
    public static MenuItemDto From(MenuItem menuItem) => new(menuItem.Id, menuItem.Name, menuItem.Price);
}
