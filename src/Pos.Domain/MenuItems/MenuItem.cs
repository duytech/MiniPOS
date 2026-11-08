using Pos.Domain.Common;

namespace Pos.Domain.MenuItems;

public class MenuItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Money Price { get; set; }
}
