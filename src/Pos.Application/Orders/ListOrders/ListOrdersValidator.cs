using FluentValidation;

namespace Pos.Application.Orders.ListOrders;

internal sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public const int MaxPageSize = 100;

    public ListOrdersValidator()
    {
        RuleFor(q => q.Status).IsInEnum();
        RuleFor(q => q.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(1)
            .Must((q, page) => (long)(page - 1) * q.PageSize <= int.MaxValue)
            .WithMessage("'Page' is too large for this page size.");
    }
}
