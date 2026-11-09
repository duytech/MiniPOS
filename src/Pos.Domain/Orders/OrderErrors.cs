using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>Order errors shared by several use cases.</summary>
public static class OrderErrors
{
    public static Error NotOpen(OrderStatus status) =>
        new(ErrorCode.OrderNotOpen, $"The order is {status}; only an open order can be changed.");

    /// <summary>Applying a fixed discount above the subtotal.</summary>
    public static Error DiscountExceedsSubtotal(Money discount, Money subtotal) =>
        new(ErrorCode.DiscountExceedsSubtotal, $"A discount of {discount} is more than the subtotal of {subtotal}.");

    /// <summary>An item change, named by <paramref name="change"/>, that would leave a subtotal below the fixed discount.</summary>
    public static Error DiscountExceedsNewSubtotal(string change, Money discount, Money newSubtotal) =>
        new(ErrorCode.DiscountExceedsSubtotal,
            $"{change} would leave a subtotal of {newSubtotal}, less than the discount of {discount}. " +
            "Change or remove the discount first.");

    public static Error ItemNotOnOrder(Guid itemId) =>
        new(ErrorCode.ItemNotOnOrder, $"Item {itemId} is not on this order.");
}
