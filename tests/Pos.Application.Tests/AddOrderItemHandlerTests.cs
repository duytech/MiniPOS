using Pos.Application.Orders.AddOrderItem;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

public class AddOrderItemHandlerTests
{
    private static readonly MenuItem Burger = OrderActions.Item("Burger", 12.99m);
    private static readonly MenuItem Fries = OrderActions.Item("Fries", 3.35m);

    private readonly OrderActions _act = new();

    [Fact]
    public async Task Adds_the_item_at_the_menu_price_and_returns_domain_totals()
    {
        var order = _act.NewOrder();

        var result = (await _act.AddItem(order, OrderActions.Item("Pho", 7.70m), 2)).Data;

        var line = Assert.Single(result.Lines);
        Assert.Equal("Pho", line.Name);
        Assert.Equal(Money.Usd(7.70m), line.UnitPrice);
        Assert.Equal(Money.Usd(15.40m), line.LineTotal);
        Assert.Equal(Money.Usd(15.40m), result.Subtotal);
        Assert.Equal(Money.Usd(15.40m), result.Total);
        Assert.Equal(1, _act.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        var result = await _act.AddItem(new Order { Id = Guid.NewGuid() }, Burger, 1);

        Assert.Equal(ErrorCode.OrderNotFound, result.Error.Code);
        Assert.Equal(0, _act.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Unknown_menu_item_is_not_found()
    {
        var order = _act.NewOrder();

        var result = await new AddOrderItemHandler(_act.Orders, _act.MenuItems, _act.UnitOfWork)
            .HandleAsync(new AddOrderItemCommand(order.Id, Guid.NewGuid(), 1, null), CancellationToken.None);

        Assert.Equal(ErrorCode.MenuItemNotFound, result.Error.Code);
        Assert.Equal(0, _act.UnitOfWork.SaveCount);
    }

    [Fact]
    public async Task Failed_save_is_returned()
    {
        var order = _act.NewOrder();
        _act.UnitOfWork.SaveResult = Result.Fail(new Error(ErrorCode.ConcurrentChange, "Changed."));

        var result = await _act.AddItem(order, Burger, 1);

        Assert.Equal(ErrorCode.ConcurrentChange, result.Error.Code);
    }

    [Fact]
    public async Task Identical_line_increases_quantity()
    {
        var order = _act.NewOrder();

        await _act.AddItem(order, Burger, 1, "no onions");
        var result = (await _act.AddItem(order, Burger, 2, "no onions")).Data;

        Assert.Equal(3, Assert.Single(order.Lines).Quantity);
        Assert.Equal(Money.Usd(38.97m), result.Total);
    }

    [Fact]
    public async Task Notes_are_trimmed_and_blank_notes_count_as_no_notes()
    {
        var order = _act.NewOrder();

        await _act.AddItem(order, Burger, 1, null);
        await _act.AddItem(order, Burger, 1, "   ");
        await _act.AddItem(order, Burger, 1, "");
        await _act.AddItem(order, Burger, 1, " no onions ");

        Assert.Equal([3, 1], order.Lines.Select(l => l.Quantity));
        Assert.Equal([null, "no onions"], order.Lines.Select(l => l.Notes));
    }

    [Fact]
    public async Task Different_notes_create_a_new_line()
    {
        var order = _act.NewOrder();

        await _act.AddItem(order, Burger, 1, "no onions");
        await _act.AddItem(order, Burger, 1, null);

        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public async Task Different_menu_item_creates_a_new_line()
    {
        var order = _act.NewOrder();

        await _act.AddItem(order, Burger, 1);
        await _act.AddItem(order, Fries, 1);

        Assert.Equal(2, order.Lines.Count);
    }

    [Fact]
    public async Task A_new_menu_price_creates_a_new_line_and_keeps_the_snapshotted_price()
    {
        var order = _act.NewOrder();
        var pho = OrderActions.Item("Pho", 7.70m);
        await _act.AddItem(order, pho, 1);

        pho.Price = Money.Usd(8.00m);
        var result = (await _act.AddItem(order, pho, 1)).Data;

        Assert.Equal([Money.Usd(7.70m), Money.Usd(8.00m)], result.Lines.Select(l => l.UnitPrice));
        Assert.Equal(Money.Usd(15.70m), result.Total);
    }

    [Fact]
    public async Task Line_quantity_overflow_is_an_error_and_nothing_is_saved()
    {
        var order = _act.NewOrder();
        await _act.AddItem(order, Burger, int.MaxValue);
        var savesBefore = _act.UnitOfWork.SaveCount;

        var result = await _act.AddItem(order, Burger, 1);

        Assert.Equal(ErrorCode.LineQuantityTooLarge, result.Error.Code);
        Assert.Equal(int.MaxValue, Assert.Single(order.Lines).Quantity);
        Assert.Equal(savesBefore, _act.UnitOfWork.SaveCount);
    }
}
