using FluentValidation;

namespace Pos.Application.Orders.SplitOrderEvenly;

internal sealed class SplitOrderEvenlyValidator : AbstractValidator<SplitOrderEvenlyQuery>
{
    /// <summary>Business limit: a bill is split evenly between at most 20 people.</summary>
    public const int MaxParts = 20;

    public SplitOrderEvenlyValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
        RuleFor(q => q.Parts).InclusiveBetween(1, MaxParts);
    }
}
