using FluentValidation;

namespace Pos.Application.Orders.SaveSplit;

/// <summary>Shape only; the number of parts and which items they hold are checked by the order.</summary>
internal sealed class SaveSplitValidator : AbstractValidator<SaveSplitCommand>
{
    public SaveSplitValidator()
    {
        RuleFor(c => c.Parts).NotNull();
        RuleForEach(c => c.Parts).NotNull()
            .ForEach(itemId => itemId.NotEmpty());
    }
}
