using FluentValidation;

namespace Pos.Application.Orders.CancelOrder;

internal sealed class CancelOrderValidator : AbstractValidator<CancelOrderCommand>
{
    public CancelOrderValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}
