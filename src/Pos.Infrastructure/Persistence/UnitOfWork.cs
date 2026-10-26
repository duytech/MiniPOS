using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pos.Application.Common;

namespace Pos.Infrastructure.Persistence;

internal sealed class UnitOfWork(PosDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException("The data was changed by another request. Reload and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation
        })
        {
            throw new ConflictException("The change conflicts with existing data.", ex);
        }
    }
}
