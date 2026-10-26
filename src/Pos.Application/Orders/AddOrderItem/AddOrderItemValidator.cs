using FluentValidation;

namespace Pos.Application.Orders.AddOrderItem;

internal sealed class AddOrderItemValidator : AbstractValidator<AddOrderItemCommand>
{
    public AddOrderItemValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.MenuItemId).NotEmpty();
        RuleFor(c => c.Quantity).GreaterThan(0);
    }
}
