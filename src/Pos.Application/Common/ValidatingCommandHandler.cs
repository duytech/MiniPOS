using FluentValidation;

namespace Pos.Application.Common;

/// <summary>Runs the command's validators before the handler; throws <see cref="ValidationException"/> on failure.</summary>
internal sealed class ValidatingCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
{
    public async Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            await validator.ValidateAndThrowAsync(command, cancellationToken);
        }

        return await inner.HandleAsync(command, cancellationToken);
    }
}
