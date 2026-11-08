using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.Common;

/// <summary>Runs the command's validators before the handler; fails with <see cref="ErrorCode.ValidationFailed"/>.</summary>
internal sealed class ValidatingCommandHandler<TCommand, TResult>(
    ICommandHandler<TCommand, TResult> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResult>
{
    public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
        await validators.FirstErrorAsync(command, cancellationToken) is { } error
            ? Result.Fail<TResult>(error)
            : await inner.HandleAsync(command, cancellationToken);
}
