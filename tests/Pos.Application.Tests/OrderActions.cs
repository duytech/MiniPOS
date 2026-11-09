using Microsoft.Extensions.Logging;
using Pos.Application.Orders;
using Pos.Application.Orders.AddOrderItem;
using Pos.Application.Orders.ApplyDiscount;
using Pos.Application.Orders.CancelOrder;
using Pos.Application.Orders.ChangeOrderMenuItemQuantity;
using Pos.Application.Orders.GetSplit;
using Pos.Application.Orders.PayOrder;
using Pos.Application.Orders.PreviewSplit;
using Pos.Application.Orders.RemoveDiscount;
using Pos.Application.Orders.RemoveOrderMenuItem;
using Pos.Application.Orders.RemoveSplit;
using Pos.Application.Orders.RemoveTip;
using Pos.Application.Orders.SaveSplit;
using Pos.Application.Orders.SetPaymentMethod;
using Pos.Application.Orders.SetTip;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Application.Tests;

/// <summary>Runs the real order handlers against in-memory fakes, one call per use case.</summary>
internal sealed class OrderActions
{
    public FakeOrderRepository Orders { get; } = new();
    public FakeMenuItemRepository MenuItems { get; } = new();
    public FixedTimeProvider Time { get; } = new();

    /// <summary>What the handlers logged, in order.</summary>
    public List<(LogLevel Level, Exception? Exception)> Logs { get; } = [];

    public ILogger<T> Logger<T>() => new RecordingLogger<T>(Logs);

    /// <summary>An open, empty order in the repository, paid in cash.</summary>
    public Order NewOrder(Percentage? taxRate = null, Percentage? cardSurchargeRate = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
            Currency = Currency.USD,
            TaxRate = taxRate ?? Percentage.Zero,
            CardSurchargeRate = cardSurchargeRate ?? Percentage.Zero,
            PaymentMethod = PaymentMethod.Cash,
        };
        Orders.Add(order);
        return order;
    }

    public static MenuItem Item(string name, decimal price) =>
        new() { Id = Guid.NewGuid(), Name = name, Price = Money.Usd(price) };

    public Task<Result<OrderDto>> AddItem(Order order, MenuItem menuItem, int quantity, string? notes = null)
    {
        MenuItems.MenuItems.TryAdd(menuItem.Id, menuItem);
        return new AddOrderItemHandler(Orders, MenuItems, Logger<AddOrderItemHandler>())
            .HandleAsync(new AddOrderItemCommand(order.Id, menuItem.Id, quantity, notes), CancellationToken.None);
    }

    /// <summary>Adds a new item of a new menu item and returns its id.</summary>
    public async Task<Guid> AddPricedItem(Order order, decimal price, int quantity = 1)
    {
        var added = await AddItem(order, Item($"Item {order.Items.Count}", price), quantity);
        return added.Data!.Items[^1].Id;
    }

    public Task<Result<OrderDto>> RemoveItem(Order order, Guid itemId) =>
        new RemoveOrderMenuItemHandler(Orders, Logger<RemoveOrderMenuItemHandler>())
            .HandleAsync(new RemoveOrderMenuItemCommand(order.Id, itemId), CancellationToken.None);

    public Task<Result<OrderDto>> ChangeQuantity(Order order, Guid itemId, int quantity) =>
        new ChangeOrderMenuItemQuantityHandler(Orders, Logger<ChangeOrderMenuItemQuantityHandler>())
            .HandleAsync(new ChangeOrderMenuItemQuantityCommand(order.Id, itemId, quantity), CancellationToken.None);

    public Task<Result<OrderDto>> ApplyPercentDiscount(Order order, decimal percent) =>
        new ApplyDiscountHandler(Orders, Logger<ApplyDiscountHandler>())
            .HandleAsync(new ApplyDiscountCommand(order.Id, new Percentage(percent), null), CancellationToken.None);

    public Task<Result<OrderDto>> ApplyFixedDiscount(Order order, decimal amount) =>
        new ApplyDiscountHandler(Orders, Logger<ApplyDiscountHandler>())
            .HandleAsync(new ApplyDiscountCommand(order.Id, null, Money.Usd(amount)), CancellationToken.None);

    public Task<Result<OrderDto>> RemoveDiscount(Order order) =>
        new RemoveDiscountHandler(Orders, Logger<RemoveDiscountHandler>()).HandleAsync(new RemoveDiscountCommand(order.Id), CancellationToken.None);

    public Task<Result<OrderDto>> SetTip(Order order, decimal percent) =>
        new SetTipHandler(Orders, Logger<SetTipHandler>())
            .HandleAsync(new SetTipCommand(order.Id, new Percentage(percent)), CancellationToken.None);

    public Task<Result<OrderDto>> RemoveTip(Order order) =>
        new RemoveTipHandler(Orders, Logger<RemoveTipHandler>()).HandleAsync(new RemoveTipCommand(order.Id), CancellationToken.None);

    public Task<Result<OrderDto>> SetPaymentMethod(Order order, PaymentMethod paymentMethod) =>
        new SetPaymentMethodHandler(Orders, Logger<SetPaymentMethodHandler>())
            .HandleAsync(new SetPaymentMethodCommand(order.Id, paymentMethod), CancellationToken.None);

    /// <summary>Pays or cancels the order at <paramref name="at"/> (the fake clock's time when null).</summary>
    public Task<Result<OrderDto>> Close(Order order, OrderStatus status, DateTimeOffset? at = null)
    {
        if (at is { } time)
        {
            Time.Now = time;
        }

        return status switch
        {
            OrderStatus.Paid => new PayOrderHandler(Orders, Time, Logger<PayOrderHandler>())
                .HandleAsync(new PayOrderCommand(order.Id), CancellationToken.None),
            OrderStatus.Cancelled => new CancelOrderHandler(Orders, Time, Logger<CancelOrderHandler>())
                .HandleAsync(new CancelOrderCommand(order.Id), CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Paid or Cancelled"),
        };
    }

    public Task<Result<OrderSplitDto>> PreviewSplit(Order order, params Guid[][] parts) =>
        new PreviewSplitHandler(Orders).HandleAsync(new PreviewSplitQuery(order.Id, parts), CancellationToken.None);

    public Task<Result<OrderSplitDto>> SaveSplit(Order order, params Guid[][] parts) =>
        new SaveSplitHandler(Orders, Logger<SaveSplitHandler>()).HandleAsync(new SaveSplitCommand(order.Id, parts), CancellationToken.None);

    public Task<Result<bool?>> RemoveSplit(Order order) =>
        new RemoveSplitHandler(Orders, Logger<RemoveSplitHandler>()).HandleAsync(new RemoveSplitCommand(order.Id), CancellationToken.None);

    public Task<Result<OrderSplitDto>> GetSplit(Order order) =>
        new GetSplitHandler(Orders).HandleAsync(new GetSplitQuery(order.Id), CancellationToken.None);
}

internal sealed class FixedTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 27, 9, 15, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}
