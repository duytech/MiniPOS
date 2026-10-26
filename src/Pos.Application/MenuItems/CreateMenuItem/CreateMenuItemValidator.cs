using FluentValidation;
using Pos.Application.Common;

namespace Pos.Application.MenuItems.CreateMenuItem;

internal sealed class CreateMenuItemValidator : AbstractValidator<CreateMenuItemCommand>
{
    public CreateMenuItemValidator()
    {
        RuleFor(c => c.Name).NotEmpty();
        RuleFor(c => c.Price.Amount).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(MoneyLimits.MaxAmount)
            .OverridePropertyName(nameof(CreateMenuItemCommand.Price));
    }
}
