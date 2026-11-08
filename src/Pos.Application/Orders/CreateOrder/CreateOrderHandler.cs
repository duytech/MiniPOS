using Pos.Application.Common;
using Pos.Domain.Common;
using Pos.Domain.Orders;

namespace Pos.Application.Orders.CreateOrder;

public sealed class CreateOrderHandler(IOrderRepository orders, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    PricingOptions pricing)
    : ICommandHandler<CreateOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Open,
            CreatedAt = timeProvider.GetUtcNow().ToUniversalTime(),
            Currency = Currency.USD,
            TaxRate = new Percentage(pricing.TaxRatePercent),
            CardSurchargeRate = new Percentage(pricing.CardSurchargePercent),
            PaymentMethod = PaymentMethod.Cash,
        };
        orders.Add(order);
        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Ok(OrderDto.From(order)) : Result.Fail<OrderDto>(saved.Error);
    }
}
