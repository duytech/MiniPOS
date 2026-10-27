using FluentValidation;

namespace Pos.Application.Orders.SplitOrderEvenly;

internal sealed class SplitOrderEvenlyValidator : AbstractValidator<SplitOrderEvenlyQuery>
{
    public SplitOrderEvenlyValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
        RuleFor(q => q.Parts).GreaterThanOrEqualTo(1);
    }
}
