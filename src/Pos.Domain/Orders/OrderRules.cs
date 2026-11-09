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
    /// A fixed discount can never be more than the subtotal. <paramref name="change"/> names the item change that would
    /// leave <paramref name="subtotal"/>, or is null when the discount itself is being applied.
    /// </summary>
    public static Error? FixedDiscountError(Money? fixedDiscount, Money subtotal, string? change)
    {
        if (fixedDiscount is not { } discount || discount.Amount <= subtotal.Amount)
        {
            return null;
        }

        return new Error(ErrorCode.DiscountExceedsSubtotal, change is null
            ? $"A discount of {discount} is more than the subtotal of {subtotal}."
            : $"{change} would leave a subtotal of {subtotal}, less than the discount of {discount}. " +
              "Change or remove the discount first.");
    }
}
