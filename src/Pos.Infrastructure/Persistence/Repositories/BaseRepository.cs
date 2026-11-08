using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pos.Application.Common;

namespace Pos.Infrastructure.Persistence.Repositories;

internal abstract class BaseRepository(PosDbContext db)
{
    private const string ChangedByAnotherRequest = "The data was changed by another request. Reload and try again.";

    private readonly PosDbContext _db = db;

    /// <summary>
    /// Throws <see cref="ConcurrentChangeException"/> or <see cref="DataConflictException"/> for the expected failures;
    /// anything else is rethrown as it is.
    /// </summary>
    protected async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrentChangeException(ChangedByAnotherRequest, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation
        })
        {
            throw new DataConflictException("The change conflicts with existing data.", ex);
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // Two requests changing the same order took row or index locks in a different order (e.g. inserting the
            // parts of a saved split); Postgres aborts one of them. It lost the race, like a concurrency-token conflict.
            throw new ConcurrentChangeException(ChangedByAnotherRequest, ex);
        }
    }

    /// <summary>
    /// Npgsql reports a deadlock as a transient failure, so the execution strategy wraps the DbUpdateException in an
    /// InvalidOperationException: look through the inner exceptions.
    /// </summary>
    private static bool IsDeadlock(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
            {
                return true;
            }
        }

        return false;
    }
}
