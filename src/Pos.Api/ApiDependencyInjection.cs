using System.Text.Json.Serialization;
using Pos.Api.Errors;
using Pos.Api.Json;

namespace Pos.Api;

public static class ApiDependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
                o.JsonSerializerOptions.Converters.Add(new PercentageJsonConverter());
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddOpenApi();
        return services;
    }
}
