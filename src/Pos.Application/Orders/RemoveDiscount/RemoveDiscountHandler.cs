using Pos.Application.Common;

namespace Pos.Application.Orders.RemoveDiscount;

public sealed class RemoveDiscountHandler(IOrderRepository orders, IUnitOfWork unitOfWork)
    : ICommandHandler<RemoveDiscountCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(RemoveDiscountCommand command, CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new NotFoundException("Order", command.OrderId);

        order.RemoveDiscount();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}
