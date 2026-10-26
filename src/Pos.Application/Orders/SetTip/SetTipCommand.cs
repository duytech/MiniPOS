using Pos.Domain.Common;

namespace Pos.Application.Orders.SetTip;

/// <summary>Tip as a percentage of the pre-tax amount (subtotal after discount).</summary>
public sealed record SetTipCommand(Guid OrderId, Percentage Rate);
