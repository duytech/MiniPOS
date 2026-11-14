using System.Diagnostics;
using Npgsql;

namespace Pos.Api.IntegrationTests;

/// <summary>
/// Makes parallel requests on one order really race, every time. Releasing them together is not enough: when the first
/// one commits before the others load the order, they see the new state (e.g. OrderNotOpen for a paid order) and the
/// concurrency token is never exercised. Here the test holds a row lock on the order (<c>FOR NO KEY UPDATE</c>: plain
/// reads and the foreign-key checks of inserted items or split parts pass, the order's <c>UPDATE</c> waits). Every
/// request loads the same order version and then blocks on its write. Once all of them wait, the lock is released
/// without changing the row: one write wins, and every other one fails the <c>xmin</c> check.
/// </summary>
internal static class OrderRace
{
    private static readonly TimeSpan BlockTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Sessions waiting, directly or through another waiting session, on the gate's lock.</summary>
    private const string BlockedSessionsSql = """
        WITH RECURSIVE blocked AS (
            SELECT pid FROM pg_stat_activity WHERE @gate = ANY(pg_blocking_pids(pid))
            UNION
            SELECT a.pid FROM pg_stat_activity a JOIN blocked b ON b.pid = ANY(pg_blocking_pids(a.pid))
        )
        SELECT COUNT(*)::int FROM blocked
        """;

    public static async Task<T[]> RunAsync<T>(
        string connectionString, Guid orderId, IEnumerable<Func<Task<T>>> requests)
    {
        await using var gate = new NpgsqlConnection(connectionString);
        await gate.OpenAsync();
        await using var lockTransaction = await gate.BeginTransactionAsync();
        await using (var lockOrder = new NpgsqlCommand(
            "SELECT 1 FROM orders WHERE id = @o FOR NO KEY UPDATE", gate, lockTransaction))
        {
            lockOrder.Parameters.AddWithValue("o", orderId);
            Assert.Equal(1, await lockOrder.ExecuteScalarAsync());
        }

        var running = requests.Select(send => Task.Run(send)).ToList();
        var isAllBlocked = await IsAllBlockedWithinTimeoutAsync(connectionString, gate.ProcessID, running);
        await lockTransaction.RollbackAsync();
        var results = await Task.WhenAll(running);
        Assert.True(isAllBlocked,
            $"Only some of the {running.Count} requests reached the order's write within {BlockTimeout}.");
        return results;
    }

    /// <summary>
    /// Polls from a second connection: pg_stat_activity is a snapshot per transaction, so the gate's own transaction
    /// would keep seeing the first answer. Stops early when a request finished without waiting.
    /// </summary>
    private static async Task<bool> IsAllBlockedWithinTimeoutAsync<T>(
        string connectionString, int gatePid, List<Task<T>> running)
    {
        await using var monitor = new NpgsqlConnection(connectionString);
        await monitor.OpenAsync();
        await using var countBlocked = new NpgsqlCommand(BlockedSessionsSql, monitor);
        countBlocked.Parameters.AddWithValue("gate", gatePid);

        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < BlockTimeout && !running.Exists(t => t.IsCompleted))
        {
            if ((int)(await countBlocked.ExecuteScalarAsync())! == running.Count)
            {
                return true;
            }

            await Task.Delay(20);
        }

        return false;
    }
}
