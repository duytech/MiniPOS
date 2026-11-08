using Pos.Domain.Common;

namespace Pos.Domain.Orders;

/// <summary>
/// Mapped to the database: properties only. Use-case logic lives in the handlers; pricing, splitting and the shared
/// rules live in <see cref="OrderPricing"/>, <see cref="OrderSplitting"/> and <see cref="OrderRules"/>.
/// </summary>
public class Order
{
    public Guid Id { get; set; }

    /// <summary>Sequential number staff use to refer to the order; assigned by the database when the order is saved.</summary>
    public int Number { get; set; }

    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the order was paid; null while it is open or when it was cancelled.</summary>
    public DateTimeOffset? PaidAt { get; set; }

    /// <summary>When the order was cancelled; null while it is open or when it was paid.</summary>
    public DateTimeOffset? CancelledAt { get; set; }
    public Currency Currency { get; set; }

    /// <summary>Sales tax rate in effect when the order was created; later rate changes don't affect it.</summary>
    public Percentage TaxRate { get; set; }

    /// <summary>Card surcharge rate in effect when the order was created; charged only when paying by card.</summary>
    public Percentage CardSurchargeRate { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    /// <summary>Tip as a percentage of the pre-tax amount (subtotal after discount); null means no tip.</summary>
    public Percentage? TipRate { get; set; }

    /// <summary>Discount as a percentage of the subtotal; at most one of this and <see cref="DiscountAmount"/> is set.</summary>
    public Percentage? DiscountRate { get; set; }

    /// <summary>Discount as a fixed amount; at most one of this and <see cref="DiscountRate"/> is set.</summary>
    public Money? DiscountAmount { get; set; }

    public List<OrderLine> Lines { get; set; } = [];

    /// <summary>
    /// The saved split by item (line assignment only, in part order); empty when there is none. A saved split only
    /// fits the order it was made for: every real change of the order clears it.
    /// </summary>
    public List<SavedSplitPart> SavedSplitParts { get; set; } = [];
}
