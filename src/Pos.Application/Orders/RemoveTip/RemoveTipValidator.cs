using FluentValidation;

namespace Pos.Application.Orders.RemoveTip;

internal sealed class RemoveTipValidator : AbstractValidator<RemoveTipCommand>
{
    public RemoveTipValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}
