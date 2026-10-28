using FluentValidation;

namespace Pos.Application.Orders.RemoveSplit;

internal sealed class RemoveSplitValidator : AbstractValidator<RemoveSplitCommand>
{
    public RemoveSplitValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}
