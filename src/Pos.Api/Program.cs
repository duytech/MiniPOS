using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Pos.Api.Errors;
using Pos.Api.Json;
using Pos.Application;
using Pos.Application.Orders;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddSingleton(builder.Configuration.GetSection(PricingOptions.SectionName).Get<PricingOptions>()
    ?? throw new InvalidOperationException($"The '{PricingOptions.SectionName}' configuration section is missing."));
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new PercentageJsonConverter());
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
}

app.MapControllers();

app.Run();

public partial class Program;
