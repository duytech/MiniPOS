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
/// Outside Development the API migrates on startup only when <c>Database:MigrateOnStartup</c> is true (the Docker
/// image sets it). Each test starts the app as Production on its own database, which does not exist yet.
/// </summary>
public class StartupMigrationTests
{
    [Fact]
    public async Task Production_with_MigrateOnStartup_applies_every_migration()
    {
        await using var app = new ProductionApp(isMigrateOnStartup: true);
        try
        {
            app.CreateClient();

            using var scope = app.Services.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<PosDbContext>().Database;
            Assert.NotEmpty(await database.GetAppliedMigrationsAsync());
            Assert.Empty(await database.GetPendingMigrationsAsync());
        }
        finally
        {
            await app.DeleteDatabaseAsync();
        }
    }

    [Fact]
    public async Task Production_without_MigrateOnStartup_leaves_the_database_alone()
    {
        await using var app = new ProductionApp(isMigrateOnStartup: false);
        try
        {
            app.CreateClient();

            using var scope = app.Services.CreateScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.CanConnectAsync());
        }
        finally
        {
            await app.DeleteDatabaseAsync();
        }
    }

    private sealed class ProductionApp(bool isMigrateOnStartup) : WebApplicationFactory<Program>
    {
        private readonly string _connectionString = new NpgsqlConnectionStringBuilder(ServerConnectionString())
        {
            Database = $"pos_test_{Guid.NewGuid():N}",
        }.ConnectionString;

        public async Task DeleteDatabaseAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<PosDbContext>().Database.EnsureDeletedAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting($"ConnectionStrings:{DependencyInjection.ConnectionStringName}", _connectionString);
            builder.UseSetting("Database:MigrateOnStartup", isMigrateOnStartup ? "true" : "false");
        }

        private static string ServerConnectionString() =>
            new ConfigurationBuilder()
                .AddUserSecrets<PosApiFactory>()
                .AddEnvironmentVariables()
                .Build()
                .GetConnectionString(DependencyInjection.ConnectionStringName)
            ?? throw new InvalidOperationException(
                "Integration tests need ConnectionStrings:Pos (user-secrets of Pos.Api.IntegrationTests or the " +
                "ConnectionStrings__Pos environment variable) pointing at a Postgres server.");
    }
}
