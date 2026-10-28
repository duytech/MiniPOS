using FluentValidation;

namespace Pos.Application.Orders.GetSplit;

internal sealed class GetSplitValidator : AbstractValidator<GetSplitQuery>
{
    public GetSplitValidator()
    {
        RuleFor(q => q.OrderId).NotEmpty();
    }
}
