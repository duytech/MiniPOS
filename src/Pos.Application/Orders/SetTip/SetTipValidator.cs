using FluentValidation;
using Pos.Application.Common;

namespace Pos.Application.Orders.SetTip;

internal sealed class SetTipValidator : AbstractValidator<SetTipCommand>
{
    public SetTipValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Rate.Value).LessThanOrEqualTo(PercentageLimits.MaxRate)
            .OverridePropertyName(nameof(SetTipCommand.Rate));
    }
}
