using FluentValidation;
using Pos.Domain.Common;

namespace Pos.Application.Common;

internal static class ValidatorExtensions
{
    /// <summary>The failures of the first validator that rejects the request; null when all accept it.</summary>
    public static async Task<ValidationError?> FirstErrorAsync<TRequest>(
        this IEnumerable<IValidator<TRequest>> validators, TRequest request, CancellationToken cancellationToken)
    {
        foreach (var validator in validators)
        {
            var validation = await validator.ValidateAsync(request, cancellationToken);
            if (!validation.IsValid)
            {
                return new ValidationError(validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
            }
        }

        return null;
    }
}
