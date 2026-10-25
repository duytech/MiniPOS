using Pos.Domain.MenuItems;

namespace Pos.Application.MenuItems;

public sealed record CreateMenuItemRequest(string Name, decimal Price, bool IsAvailable = true);

public sealed record MenuItemDto(Guid Id, string Name, decimal Price, bool IsAvailable)
{
    public static MenuItemDto From(MenuItem m) => new(m.Id, m.Name, m.Price, m.IsAvailable);
}
