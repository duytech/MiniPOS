using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Runs the API against a throw-away database on the Postgres server named by <c>ConnectionStrings:Pos</c>
/// (user-secrets of this project or the <c>ConnectionStrings__Pos</c> environment variable).
/// </summary>
public sealed class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;

    public PosApiFactory()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<PosApiFactory>()
            .AddEnvironmentVariables()
            .Build();
        var serverConnectionString = configuration.GetConnectionString(DependencyInjection.ConnectionStringName)
            ?? throw new InvalidOperationException(
                "Integration tests need ConnectionStrings:Pos (user-secrets of Pos.Api.IntegrationTests or the " +
                "ConnectionStrings__Pos environment variable) pointing at a Postgres server.");

        _connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString)
        {
            Database = $"pos_test_{Guid.NewGuid():N}",
        }.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _connectionString);
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }
}
