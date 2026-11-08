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

    public static OrderLine AddLine(Order order, decimal unitPrice, int quantity = 1)
    {
        var line = new OrderLine
        {
            Id = Guid.NewGuid(),
            MenuItemId = Guid.NewGuid(),
            Name = $"Item {order.Lines.Count}",
            UnitPrice = Money.Usd(unitPrice),
            Quantity = quantity,
        };
        order.Lines.Add(line);
        return line;
    }

    /// <summary>An order with one line per price; returns the line ids in order.</summary>
    public static (Order Order, Guid[] LineIds) WithLines(Percentage taxRate, Percentage cardSurchargeRate, params decimal[] prices)
    {
        var order = New(taxRate, cardSurchargeRate);
        var lineIds = prices.Select(price => AddLine(order, price).Id).ToArray();
        return (order, lineIds);
    }
}
