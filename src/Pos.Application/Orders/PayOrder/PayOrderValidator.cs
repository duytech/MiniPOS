using FluentValidation;

namespace Pos.Application.Orders.PayOrder;

internal sealed class PayOrderValidator : AbstractValidator<PayOrderCommand>
{
    public PayOrderValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}
