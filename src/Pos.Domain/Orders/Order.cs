using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Domain.Orders;

public class Order
{
    public const int MaxQuantityPerLine = 1000;

    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public OrderStatus Status { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    /// <summary>Sum of all line totals (unit price x quantity).</summary>
    public decimal Total => _items.Sum(i => i.LineTotal);

    // For EF Core.
    private Order() { }

    public Order(DateTime createdAtUtc)
    {
        Id = Guid.NewGuid();
        CreatedAtUtc = DateTime.SpecifyKind(createdAtUtc, DateTimeKind.Utc);
        Status = OrderStatus.Open;
    }

    /// <summary>
    /// Adds a menu item to the order. Adding the same menu item again increases the
    /// quantity of the existing line instead of creating a new one.
    /// </summary>
    public OrderItem AddItem(MenuItem menuItem, int quantity)
    {
        ArgumentNullException.ThrowIfNull(menuItem);

        if (Status != OrderStatus.Open)
            throw new DomainException($"Cannot add items to an order with status '{Status}'.");
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");
        if (!menuItem.IsAvailable)
            throw new DomainException($"Menu item '{menuItem.Name}' is not available.");

        var existing = _items.FirstOrDefault(i => i.MenuItemId == menuItem.Id);
        var newQuantity = (existing?.Quantity ?? 0) + quantity;
        if (newQuantity > MaxQuantityPerLine)
            throw new DomainException($"Quantity per item cannot exceed {MaxQuantityPerLine}.");

        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return existing;
        }

        var item = new OrderItem(Id, menuItem.Id, menuItem.Name, menuItem.Price, quantity);
        _items.Add(item);
        return item;
    }
}
