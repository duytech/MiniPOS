using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>Order errors shared by several use cases.</summary>
public static class OrderErrors
{
    public static Error NotOpen(OrderStatus status) =>
        new(ErrorCode.OrderNotOpen, $"The order is {status}; only an open order can be changed.");

    public static Error ItemNotOnOrder(Guid itemId) =>
        new(ErrorCode.ItemNotOnOrder, $"Item {itemId} is not on this order.");
}
