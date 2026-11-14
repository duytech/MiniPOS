using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Json;
using Pos.Domain.Common;

namespace Pos.Api;

public static class ApiDependencyInjection
{
    private const string InvalidInput = "The input was not valid.";

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers()
            // A request ASP.NET rejected before the action ran: malformed JSON, wrong type, missing required field.
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
            {
                var messages = context.ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? InvalidInput : e.ErrorMessage);
                var error = Error.Validation(messages);
                return new OkObjectResult(Result.Fail<object>(error));
            })
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
                o.JsonSerializerOptions.Converters.Add(new PercentageJsonConverter());
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddProblemDetails();
        return services;
    }
}
