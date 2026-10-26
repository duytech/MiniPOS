using Pos.Application.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<OrderDto> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Create(timeProvider.GetUtcNow());
        orders.Add(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return OrderDto.From(order);
    }
}
