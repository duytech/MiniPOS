using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pos.Application.MenuItems;
using Pos.Domain.MenuItems;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// An exception the API does not expect is a bug: it must answer a plain 500 ProblemDetails that says nothing about the
/// exception (CWE-209), and the error must still be logged. The menu item repository is replaced by one that throws.
/// </summary>
public class UnexpectedErrorTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    private const string OuterSecret = "outer-secret-7f3a";
    private const string InnerSecret = "inner-secret-91bc";

    private readonly ConcurrentQueue<LogEntry> _logs = new();

    private WebApplicationFactory<Program> CreateApp() => factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
        builder.ConfigureTestServices(services => services.AddScoped<IMenuItemRepository, ThrowingMenuItemRepository>());
    });

    [Fact]
    public async Task Returns_500_problem_details_without_exception_details()
    {
        await using var app = CreateApp();

        var response = await app.CreateClient().GetAsync($"/api/menu-items/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(500, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
        Assert.DoesNotContain(nameof(InvalidOperationException), body, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ThrowingMenuItemRepository), body, StringComparison.Ordinal);
        Assert.DoesNotContain(OuterSecret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(InnerSecret, body, StringComparison.Ordinal);
        Assert.DoesNotContain(" at ", body, StringComparison.Ordinal); // stack frame
        Assert.False((await response.ReadJsonAsync()).TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task The_error_is_logged()
    {
        await using var app = CreateApp();

        await app.CreateClient().GetAsync($"/api/menu-items/{Guid.NewGuid()}");

        var entry = Assert.Single(_logs, e => e.Exception is InvalidOperationException { Message: OuterSecret });
        Assert.Equal(LogLevel.Error, entry.Level);
    }

    private sealed class ThrowingMenuItemRepository : IMenuItemRepository
    {
        public Task<MenuItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(OuterSecret, new InvalidOperationException(InnerSecret));

        public Task CreateAsync(MenuItem menuItem, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record LogEntry(LogLevel Level, Exception? Exception);

    private sealed class CapturingLoggerProvider(ConcurrentQueue<LogEntry> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(entries);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, exception));
    }
}
