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

    private async Task<Order> LoadOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException("Order", orderId);
}
