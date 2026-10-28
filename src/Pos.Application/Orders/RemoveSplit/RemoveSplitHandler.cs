using Pos.Application.Common;

namespace Pos.Application.Orders.RemoveSplit;

/// <summary>
/// Checks in this order: missing order → 404, no saved split → 404, order not open → 422. Returns true once the split
/// is removed (the API answers 204 with no body).
/// </summary>
public sealed class RemoveSplitHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveSplitCommand, bool>
{
    public async Task<bool> HandleAsync(RemoveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);
        if (!order.HasSavedSplit)
        {
            throw new NotFoundException("Split", command.OrderId);
        }

        order.RemoveSplit();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
