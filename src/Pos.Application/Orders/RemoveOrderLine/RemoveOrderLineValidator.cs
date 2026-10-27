using FluentValidation;

namespace Pos.Application.Orders.RemoveOrderLine;

internal sealed class RemoveOrderLineValidator : AbstractValidator<RemoveOrderLineCommand>
{
    public RemoveOrderLineValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.LineId).NotEmpty();
    }
}
