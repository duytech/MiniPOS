using Pos.Application.Common;
using Pos.Application.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Orders;

public class OrderService(
    IOrderRepository orders,
    IMenuItemRepository menuItems,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<OrderDto> CreateAsync(CancellationToken cancellationToken = default)
    {
        var order = new Order(clock.UtcNow);
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

    public async Task<OrderDto> GetAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        return OrderDto.From(order);
    }

    public async Task<OrderDto> AddItemAsync(Guid orderId, AddOrderItemRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var order = await LoadOrderAsync(orderId, cancellationToken);
        var menuItem = await menuItems.GetByIdAsync(request.MenuItemId, cancellationToken)
            ?? throw new NotFoundException("Menu item", request.MenuItemId);

        order.AddItem(menuItem, request.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }

    public async Task<OrderTotalDto> GetTotalAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await LoadOrderAsync(orderId, cancellationToken);
        return new OrderTotalDto(order.Id, order.Items.Sum(i => i.Quantity), order.Total);
    }

    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 200;
    public const int MaxSearchLength = 100;

    /// <summary>Lists orders, newest first, optionally filtered by status and search text.</summary>
    public Task<IReadOnlyList<OrderSummaryDto>> ListAsync(ListOrdersRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return orders.ListAsync(ToFilter(request), cancellationToken);
    }

    private static OrderListFilter ToFilter(ListOrdersRequest request)
    {
        OrderStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var raw = request.Status.Trim();
            // Names only: reject numeric values such as "1" or "99" that Enum.TryParse would accept.
            if (!Enum.GetNames<OrderStatus>().Contains(raw, StringComparer.OrdinalIgnoreCase))
                throw new ValidationException(
                    $"Unknown order status '{raw}'. Allowed values: {string.Join(", ", Enum.GetNames<OrderStatus>())}.");
            status = Enum.Parse<OrderStatus>(raw, ignoreCase: true);
        }

        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        if (search is { Length: > MaxSearchLength })
            throw new ValidationException($"Search text cannot be longer than {MaxSearchLength} characters.");

        var limit = request.Limit ?? DefaultListLimit;
        if (limit is < 1 or > MaxListLimit)
            throw new ValidationException($"Limit must be between 1 and {MaxListLimit}.");

        return new OrderListFilter(status, search, limit);
    }

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
}
