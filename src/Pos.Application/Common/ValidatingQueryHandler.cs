using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.Common;

/// <summary>Runs the query's validators before the handler; fails with a <see cref="ValidationError"/>.</summary>
internal sealed class ValidatingQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
{
    public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
        await validators.FirstErrorAsync(query, cancellationToken) is { } error
            ? Result.Fail<TResult>(error)
            : await inner.HandleAsync(query, cancellationToken);
}
