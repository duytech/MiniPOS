using Pos.Domain.Common;

namespace Pos.Application.Orders.ApplyDiscount;

/// <summary>Exactly one of <paramref name="Rate"/> (percent of subtotal) or <paramref name="Amount"/> (fixed) is set.</summary>
public sealed record ApplyDiscountCommand(Guid OrderId, Percentage? Rate, Money? Amount);
