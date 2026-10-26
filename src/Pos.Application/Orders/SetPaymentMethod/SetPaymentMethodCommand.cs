using Pos.Domain.Orders;

namespace Pos.Application.Orders.SetPaymentMethod;

public sealed record SetPaymentMethodCommand(Guid OrderId, PaymentMethod PaymentMethod);
