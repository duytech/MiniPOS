using Pos.Domain.Common;

namespace Pos.Domain.MenuItems;

public class MenuItem
{
    public const int NameMaxLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; }

    // For EF Core.
    private MenuItem() { }

    public MenuItem(string name, decimal price, bool isAvailable = true)
    {
        Id = Guid.NewGuid();
        Rename(name);
        ChangePrice(price);
        IsAvailable = isAvailable;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Menu item name is required.");
        name = name.Trim();
        if (name.Length > NameMaxLength)
            throw new DomainException($"Menu item name must be at most {NameMaxLength} characters.");
        Name = name;
    }

    public void ChangePrice(decimal price)
    {
        if (price < 0)
            throw new DomainException("Menu item price cannot be negative.");
        if (decimal.Round(price, 2) != price)
            throw new DomainException("Menu item price must have at most 2 decimal places.");
        Price = price;
    }

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
}
