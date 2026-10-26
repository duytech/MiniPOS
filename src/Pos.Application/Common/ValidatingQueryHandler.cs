using FluentValidation;

namespace Pos.Application.Common;

/// <summary>Runs the query's validators before the handler; throws <see cref="ValidationException"/> on failure.</summary>
internal sealed class ValidatingQueryHandler<TQuery, TResult>(
    IQueryHandler<TQuery, TResult> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResult>
{
    public async Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            await validator.ValidateAndThrowAsync(query, cancellationToken);
        }

        return await inner.HandleAsync(query, cancellationToken);
    }
}
