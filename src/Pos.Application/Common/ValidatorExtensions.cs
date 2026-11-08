using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.Common;

internal static class ValidatorExtensions
{
    /// <summary>The failures of the first validator that rejects the request; null when all accept it.</summary>
    public static async Task<Error?> FirstErrorAsync<TRequest>(
        this IEnumerable<IValidator<TRequest>> validators, TRequest request, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return Error.Validation(validation.Errors.Select(e => e.ErrorMessage));
            }
        }

        return null;
    }
}
