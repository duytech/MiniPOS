using Pos.Api;
using Pos.Application;
using Pos.Application.Orders;
using Pos.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddSingleton(builder.Configuration.GetSection(PricingOptions.SectionName).Get<PricingOptions>()
    ?? throw new InvalidOperationException($"The '{PricingOptions.SectionName}' configuration section is missing."));
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Development always migrates; elsewhere (the Docker image) only when Database:MigrateOnStartup is true.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await app.Services.MigrateDatabaseAsync();
}

app.MapControllers();

await app.RunAsync();

// WebApplicationFactory<Program> in the integration tests needs a public Program type; nothing constructs it.
public partial class Program
{
    protected Program()
    {
    }
}
