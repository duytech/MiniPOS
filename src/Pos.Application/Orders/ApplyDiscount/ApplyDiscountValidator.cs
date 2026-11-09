using FluentValidation;
using Pos.Application.Common;

namespace Pos.Application.Orders.ApplyDiscount;

internal sealed class ApplyDiscountValidator : AbstractValidator<ApplyDiscountCommand>
{
    public ApplyDiscountValidator()
    {
        RuleFor(c => c)
            .Must(c => (c.Rate is null) != (c.Amount is null))
            .WithName("Discount")
            .WithMessage("Set exactly one of rate or amount.");
        RuleFor(c => c.Rate!.Value.Value).LessThanOrEqualTo(100m)
            .OverridePropertyName(nameof(ApplyDiscountCommand.Rate))
            .When(c => c.Rate is not null);
        RuleFor(c => c.Amount!.Value.Amount).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(MoneyLimits.MaxAmount)
            .OverridePropertyName(nameof(ApplyDiscountCommand.Amount))
            .When(c => c.Amount is not null);
    }
}
