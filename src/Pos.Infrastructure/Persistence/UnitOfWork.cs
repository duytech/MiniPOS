using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pos.Application.Common;
using Pos.Domain.Common;

namespace Pos.Infrastructure.Persistence;

internal sealed class UnitOfWork(PosDbContext db) : IUnitOfWork
{
    private const string ChangedByAnotherRequest = "The data was changed by another request. Reload and try again.";

    public async Task<Result> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Fail(new Error(ErrorCode.ConcurrentChange, ChangedByAnotherRequest));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation
        })
        {
            return Result.Fail(new Error(ErrorCode.DataConflict, "The change conflicts with existing data."));
        }
        catch (Exception ex) when (IsDeadlock(ex))
        {
            // Two requests changing the same order took row or index locks in a different order (e.g. inserting the
            // parts of a saved split); Postgres aborts one of them. It lost the race, like a concurrency-token conflict.
            return Result.Fail(new Error(ErrorCode.ConcurrentChange, ChangedByAnotherRequest));
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
