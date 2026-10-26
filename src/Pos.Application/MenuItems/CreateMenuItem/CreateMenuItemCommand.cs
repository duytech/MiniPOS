using Pos.Domain.Common;

namespace Pos.Application.MenuItems.CreateMenuItem;

public sealed record CreateMenuItemCommand(string Name, Money Price);
