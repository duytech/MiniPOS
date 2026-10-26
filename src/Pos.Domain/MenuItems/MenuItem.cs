using Pos.Domain.Common;

namespace Pos.Domain.MenuItems;

public class MenuItem
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public Money Price { get; private set; }

    private MenuItem()
    {
    }

    public MenuItem(string name, Money price)
    {
        Id = Guid.NewGuid();
        Name = name;
        Price = price;
    }
}
