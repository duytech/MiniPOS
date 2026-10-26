namespace Pos.Application.Common;

public interface IUnitOfWork
{
    /// <exception cref="ConflictException">The data was changed concurrently or a constraint was violated.</exception>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
