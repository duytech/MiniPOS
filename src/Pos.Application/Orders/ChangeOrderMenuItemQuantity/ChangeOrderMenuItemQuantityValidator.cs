using FluentValidation;

namespace Pos.Application.Orders.ChangeOrderMenuItemQuantity;

internal sealed class ChangeOrderMenuItemQuantityValidator : AbstractValidator<ChangeOrderMenuItemQuantityCommand>
{
    public ChangeOrderMenuItemQuantityValidator()
    {
        RuleFor(c => c.Quantity).GreaterThan(0);
    }
}
