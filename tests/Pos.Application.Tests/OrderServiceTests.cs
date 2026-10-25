using Pos.Application.Common;
using Pos.Application.Orders;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;

namespace Pos.Application.Tests;

public class OrderServiceTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 9, 30, 0, DateTimeKind.Utc);

    private readonly InMemoryOrderRepository _orders = new();
    private readonly InMemoryMenuItemRepository _menuItems = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly OrderService _sut;

    private readonly MenuItem _espresso = new("Espresso", 2.50m);
    private readonly MenuItem _sandwich = new("Club Sandwich", 7.90m);

    public OrderServiceTests()
    {
        _menuItems.Add(_espresso);
        _menuItems.Add(_sandwich);
        _sut = new OrderService(_orders, _menuItems, _unitOfWork, new FakeClock(Now));
    }

    [Fact]
    public async Task CreateAsync_persists_empty_open_order()
    {
        var dto = await _sut.CreateAsync();

        Assert.True(_orders.Orders.ContainsKey(dto.Id));
        Assert.Equal("Open", dto.Status);
        Assert.Equal(Now, dto.CreatedAtUtc);
        Assert.Empty(dto.Items);
        Assert.Equal(0m, dto.Total);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddItemAsync_adds_line_and_returns_updated_total()
    {
        var order = await _sut.CreateAsync();

        await _sut.AddItemAsync(order.Id, new AddOrderItemRequest(_espresso.Id, 2));
        var dto = await _sut.AddItemAsync(order.Id, new AddOrderItemRequest(_sandwich.Id, 1));

        Assert.Equal(2, dto.Items.Count);
        var espressoLine = Assert.Single(dto.Items, i => i.MenuItemId == _espresso.Id);
        Assert.Equal("Espresso", espressoLine.Name);
        Assert.Equal(2.50m, espressoLine.UnitPrice);
        Assert.Equal(2, espressoLine.Quantity);
        Assert.Equal(5.00m, espressoLine.LineTotal);
        Assert.Equal(12.90m, dto.Total);
        Assert.Equal(3, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task GetTotalAsync_returns_total_and_item_count()
    {
        var order = await _sut.CreateAsync();
        await _sut.AddItemAsync(order.Id, new AddOrderItemRequest(_espresso.Id, 3));
        await _sut.AddItemAsync(order.Id, new AddOrderItemRequest(_sandwich.Id, 2));

        var total = await _sut.GetTotalAsync(order.Id);

        Assert.Equal(order.Id, total.OrderId);
        Assert.Equal(5, total.ItemCount);
        Assert.Equal(23.30m, total.Total); // 3 x 2.50 + 2 x 7.90
    }

    [Fact]
    public async Task AddItemAsync_unknown_order_throws_not_found()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.AddItemAsync(Guid.NewGuid(), new AddOrderItemRequest(_espresso.Id, 1)));
        Assert.Contains("Order", ex.Message);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddItemAsync_unknown_menu_item_throws_not_found_and_does_not_save()
    {
        var order = await _sut.CreateAsync();

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => _sut.AddItemAsync(order.Id, new AddOrderItemRequest(Guid.NewGuid(), 1)));
        Assert.Contains("Menu item", ex.Message);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AddItemAsync_invalid_quantity_throws_domain_exception_and_does_not_save()
    {
        var order = await _sut.CreateAsync();

        await Assert.ThrowsAsync<DomainException>(
            () => _sut.AddItemAsync(order.Id, new AddOrderItemRequest(_espresso.Id, 0)));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Empty(_orders.Orders[order.Id].Items);
    }

    [Fact]
    public async Task GetAsync_unknown_order_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetAsync(Guid.NewGuid()));

    [Fact]
    public async Task GetTotalAsync_unknown_order_throws_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.GetTotalAsync(Guid.NewGuid()));
}
