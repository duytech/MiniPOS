using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Domain.Tests;

/// <summary>Builds order state directly; the use cases that change it are tested through the handlers.</summary>
internal static class TestOrders
{
    public static Order New(Percentage? taxRate = null, Percentage? cardSurchargeRate = null) => new()
    {
        Id = Guid.NewGuid(),
        Status = OrderStatus.Open,
        CreatedAt = DateTimeOffset.UtcNow,
        Currency = Currency.USD,
        TaxRate = taxRate ?? Percentage.Zero,
        CardSurchargeRate = cardSurchargeRate ?? Percentage.Zero,
        PaymentMethod = PaymentMethod.Cash,
    };

    public static OrderMenuItem AddPricedItem(Order order, decimal unitPrice, int quantity = 1)
    {
        var item = new OrderMenuItem
        {
            Id = Guid.NewGuid(),
            MenuItemId = Guid.NewGuid(),
            Name = $"Item {order.Items.Count}",
            UnitPrice = Money.Usd(unitPrice),
            Quantity = quantity,
        };
        order.Items.Add(item);
        return item;
    }

    /// <summary>An order with one item per price; returns the item ids in order.</summary>
    public static (Order Order, Guid[] ItemIds) WithItems(Percentage taxRate, Percentage cardSurchargeRate, params decimal[] prices)
    {
        var order = New(taxRate, cardSurchargeRate);
        var itemIds = prices.Select(price => AddPricedItem(order, price).Id).ToArray();
        return (order, itemIds);
    }
}
