using Pos.Domain.Common;

namespace Pos.Application.Common;

public interface IUnitOfWork
{
    /// <summary>Fails with a concurrent change or a constraint violation; those are expected, anything else throws.</summary>
    Task<Result> SaveChangesAsync(CancellationToken cancellationToken);
}
