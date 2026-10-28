using FluentValidation;

namespace Pos.Application.Orders.SaveSplit;

/// <summary>Shape only; the number of parts and which lines they hold are checked by the order.</summary>
internal sealed class SaveSplitValidator : AbstractValidator<SaveSplitCommand>
{
    public SaveSplitValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Parts).NotNull();
        RuleForEach(c => c.Parts).NotNull()
            .ForEach(lineId => lineId.NotEmpty());
    }
}
