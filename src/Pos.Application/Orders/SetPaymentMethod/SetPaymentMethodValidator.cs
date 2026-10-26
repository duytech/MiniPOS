using FluentValidation;

namespace Pos.Application.Orders.SetPaymentMethod;

internal sealed class SetPaymentMethodValidator : AbstractValidator<SetPaymentMethodCommand>
{
    public SetPaymentMethodValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.PaymentMethod).IsInEnum();
    }
}
