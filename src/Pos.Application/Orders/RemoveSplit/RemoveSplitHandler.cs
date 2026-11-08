using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.RemoveSplit;

/// <summary>
/// Checks in this order: missing order, no saved split, order not open. Returns true once the split is removed.
/// </summary>
public sealed class RemoveSplitHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveSplitCommand, bool?>
{
    public async Task<Result<bool?>> HandleAsync(RemoveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken);
        if (order is null)
        {
            return Result.Fail<bool?>(NotFoundErrors.Order(command.OrderId));
        }

        if (order.SavedSplitParts.Count == 0)
        {
            return Result.Fail<bool?>(NotFoundErrors.Split(command.OrderId));
        }

        if (OrderRules.OpenError(order) is { } notOpen)
        {
            return Result.Fail<bool?>(notOpen);
        }

        order.SavedSplitParts.Clear();

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok<bool?>(true) : Result.Fail<bool?>(saved.Error);
    }
}
