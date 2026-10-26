using FluentValidation;

namespace Pos.Application.Orders.RemoveDiscount;

internal sealed class RemoveDiscountValidator : AbstractValidator<RemoveDiscountCommand>
{
    public RemoveDiscountValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
    }
}
