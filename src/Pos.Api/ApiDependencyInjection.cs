using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Pos.Api.Contracts;
using Pos.Api.Json;

namespace Pos.Api;

public static class ApiDependencyInjection
{
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddControllers()
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = context =>
                new OkObjectResult(context.ModelState.ToResponse()))
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
