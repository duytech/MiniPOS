using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Boots the real API against a throwaway PostgreSQL database that is created
/// (and migrated on startup) for the test run and dropped afterwards.
/// Server connection defaults to the local dev database; override with the
/// POS_TEST_CONNECTION environment variable.
/// </summary>
public sealed class PosApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DefaultServerConnection =
        "Host=localhost;Port=5433;Database=pos;Username=pos;Password=pos_dev_password";

    private readonly string _serverConnection;
    private readonly string _databaseName = $"pos_test_{Guid.NewGuid():N}";

    public PosApiFactory()
    {
        _serverConnection = Environment.GetEnvironmentVariable("POS_TEST_CONNECTION") ?? DefaultServerConnection;
        TestConnectionString = new NpgsqlConnectionStringBuilder(_serverConnection) { Database = _databaseName }.ConnectionString;
    }

    public string TestConnectionString { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Pos", TestConnectionString);
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    public async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_serverConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var conn = new NpgsqlConnection(_serverConnection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", conn);
        await cmd.ExecuteNonQueryAsync();
    }
}
