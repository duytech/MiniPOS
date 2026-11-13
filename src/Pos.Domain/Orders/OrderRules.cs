using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>Order rules shared by several use cases.</summary>
public static class OrderRules
{
    /// <summary>
    /// Items, discount, tip and payment method can only change while the order is open, and only an open order can be
    /// paid or cancelled: Paid and Cancelled are final. When it is not, fail with <see cref="OrderErrors.NotOpen"/>.
    /// </summary>
    public static bool IsOpen(Order order) => order.Status == OrderStatus.Open;

    /// <summary>
    /// A fixed discount can never be more than the subtotal. When it is, fail with
    /// <see cref="OrderErrors.DiscountExceedsSubtotal"/> or <see cref="OrderErrors.DiscountExceedsNewSubtotal"/>.
    /// </summary>
    public static bool CanFitFixedDiscount(Money discount, Money subtotal) => discount.Amount <= subtotal.Amount;
}
