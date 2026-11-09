using FluentValidation;

namespace Pos.Application.Orders.PreviewSplit;

/// <summary>Shape only; the number of parts and which items they hold are checked by the order.</summary>
internal sealed class PreviewSplitValidator : AbstractValidator<PreviewSplitQuery>
{
    public PreviewSplitValidator()
    {
        RuleFor(q => q.Parts).NotNull();
        RuleForEach(q => q.Parts).NotNull()
            .ForEach(itemId => itemId.NotEmpty());
    }
}
