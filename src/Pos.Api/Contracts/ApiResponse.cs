using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pos.Domain.Common;

namespace Pos.Api.Contracts;

/// <summary>The body of every expected answer, success or failure; all of them are HTTP 200.</summary>
public sealed record ApiResponse<T>(bool IsSuccess, T? Data, ApiError? Error);

public sealed record ApiError(
    ErrorCode Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string[]>? Fields);

internal static class ApiResponses
{
    private const string InvalidInput = "The input was not valid.";

    public static ApiResponse<T> ToResponse<T>(this Result<T> result) => result.IsSuccess
        ? new ApiResponse<T>(true, result.Data, null)
        : new ApiResponse<T>(false, default, ToApiError(result.Error));

    /// <summary>A request ASP.NET rejected before the action ran: malformed JSON, wrong type, missing required field.</summary>
    public static ApiResponse<object> ToResponse(this ModelStateDictionary modelState) =>
        Result.Fail<object>(new ValidationError(modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? InvalidInput : e.ErrorMessage)
                    .ToArray())))
            .ToResponse();

    private static ApiError ToApiError(Error error) =>
        new(error.Code, error.Message, (error as ValidationError)?.Fields);
}
