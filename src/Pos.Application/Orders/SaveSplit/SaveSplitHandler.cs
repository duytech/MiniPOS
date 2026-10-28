using Pos.Application.Common;

namespace Pos.Application.Orders.SaveSplit;

public sealed class SaveSplitHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<SaveSplitCommand, OrderSplitDto>
{
    public async Task<OrderSplitDto> HandleAsync(SaveSplitCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        var parts = order.SaveSplit(command.Parts!.Select(p => (IReadOnlyCollection<Guid>)p!).ToList());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderSplitDto.From(parts);
    }
}
