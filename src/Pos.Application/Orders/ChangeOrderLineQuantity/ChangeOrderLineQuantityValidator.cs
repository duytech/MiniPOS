using FluentValidation;

namespace Pos.Application.Orders.ChangeOrderLineQuantity;

internal sealed class ChangeOrderLineQuantityValidator : AbstractValidator<ChangeOrderLineQuantityCommand>
{
    public ChangeOrderLineQuantityValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.LineId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0);
    }
}
