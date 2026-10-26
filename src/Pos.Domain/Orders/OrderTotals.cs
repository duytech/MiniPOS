using Pos.Domain.Common;

namespace Pos.Domain.Orders;

public sealed record OrderTotals(Money Subtotal, Money Discount, Money Tax, Money Tip, Money Total);
