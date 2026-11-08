using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>Checks the EF Core mapping of rates, discount, tip and payment method directly through the DbContext.</summary>
public class OrderPricingPersistenceTests(PosApiFactory factory) : IClassFixture<PosApiFactory>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rates_discount_tip_and_payment_method_survive_a_round_trip(bool percentDiscount)
    {
        var discount = percentDiscount
            ? OrderDiscount.PercentOfSubtotal(new Percentage(15m))
            : OrderDiscount.FixedAmount(Money.Usd(0.75m));
        Guid orderId;
        OrderTotals expected;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var menuItem = new MenuItem { Id = Guid.NewGuid(), Name = "Pho", Price = Money.Usd(5.00m) };
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Status = OrderStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
                Currency = Currency.USD,
                TaxRate = new Percentage(8.875m),
                CardSurchargeRate = new Percentage(3m),
                PaymentMethod = PaymentMethod.Card,
                TipRate = new Percentage(18m),
                DiscountRate = discount.Rate,
                DiscountAmount = discount.Amount,
                Lines =
                [
                    new OrderLine
                    {
                        Id = Guid.NewGuid(), MenuItemId = menuItem.Id, Name = "Pho", UnitPrice = menuItem.Price, Quantity = 1,
                    },
                ],
            };
            db.MenuItems.Add(menuItem);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            (orderId, expected) = (order.Id, OrderPricing.CalculateTotals(order));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var loaded = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);

            Assert.Equal(new Percentage(8.875m), loaded.TaxRate);
            Assert.Equal(new Percentage(3m), loaded.CardSurchargeRate);
            Assert.Equal(PaymentMethod.Card, loaded.PaymentMethod);
            Assert.Equal(new Percentage(18m), loaded.TipRate);
            Assert.Equal(discount, OrderPricing.Discount(loaded));
            Assert.Equal(expected, OrderPricing.CalculateTotals(loaded));
            Assert.Equal(Money.Usd(5.54m), OrderPricing.CalculateTotals(loaded).Total); // 4.25 + surcharge 0.13 + tax 0.39 + tip 0.77
        }
    }
}
