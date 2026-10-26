using Pos.Application.Common;
using Pos.Application.Orders.AddOrderItem;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class AddOrderItemHandlerTests
{
    private readonly FakeOrderRepository _orders = new();
    private readonly FakeMenuItemRepository _menuItems = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AddOrderItemHandler _handler;

    public AddOrderItemHandlerTests() => _handler = new AddOrderItemHandler(_orders, _menuItems, _unitOfWork);

    [Fact]
    public async Task Adds_the_item_at_the_menu_price_and_returns_domain_totals()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero);
        _orders.Add(order);
        var menuItem = new MenuItem("Pho", Money.Usd(7.70m));
        _menuItems.Add(menuItem);

        var result = await _handler.HandleAsync(
            new AddOrderItemCommand(order.Id, menuItem.Id, 2, null), CancellationToken.None);

        var line = Assert.Single(result.Lines);
        Assert.Equal("Pho", line.Name);
        Assert.Equal(Money.Usd(7.70m), line.UnitPrice);
        Assert.Equal(Money.Usd(15.40m), line.LineTotal);
        Assert.Equal(Money.Usd(15.40m), result.Subtotal);
        Assert.Equal(Money.Usd(15.40m), result.Total);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var menuItem = new MenuItem("Pho", Money.Usd(7.70m));
        _menuItems.Add(menuItem);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.HandleAsync(
            new AddOrderItemCommand(Guid.NewGuid(), menuItem.Id, 1, null), CancellationToken.None));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_menu_item_is_not_found()
    {
        var order = Order.Create(DateTimeOffset.UtcNow, Percentage.Zero);
        _orders.Add(order);

        await Assert.ThrowsAsync<NotFoundException>(() => _handler.HandleAsync(
            new AddOrderItemCommand(order.Id, Guid.NewGuid(), 1, null), CancellationToken.None));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }
}
