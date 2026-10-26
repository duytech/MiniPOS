using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pos.Domain.Common;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;
using Pos.Infrastructure.Persistence;

namespace Pos.Api.IntegrationTests;

/// <summary>No endpoint sets a discount, tip or payment method yet, so the mapping is checked through the DbContext.</summary>
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
            var menuItem = new MenuItem("Pho", Money.Usd(5.00m));
            var order = Order.Create(DateTimeOffset.UtcNow, new Percentage(8.875m), new Percentage(3m));
            order.AddItem(menuItem, 1, notes: null);
            order.ApplyDiscount(discount);
            order.SetTip(new Percentage(18m));
            order.SetPaymentMethod(PaymentMethod.Card);
            db.MenuItems.Add(menuItem);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            (orderId, expected) = (order.Id, order.CalculateTotals());
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PosDbContext>();
            var loaded = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);

            Assert.Equal(new Percentage(8.875m), loaded.TaxRate);
            Assert.Equal(new Percentage(3m), loaded.CardSurchargeRate);
            Assert.Equal(PaymentMethod.Card, loaded.PaymentMethod);
            Assert.Equal(new Percentage(18m), loaded.TipRate);
            Assert.Equal(discount, loaded.Discount);
            Assert.Equal(expected, loaded.CalculateTotals());
            Assert.Equal(Money.Usd(5.54m), loaded.CalculateTotals().Total); // 4.25 + surcharge 0.13 + tax 0.39 + tip 0.77
        }
    }
}
