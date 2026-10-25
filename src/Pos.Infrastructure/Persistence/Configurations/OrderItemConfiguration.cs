using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_order_items_unit_price_non_negative", "unit_price >= 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(i => i.OrderId).HasColumnName("order_id");
        builder.Property(i => i.MenuItemId).HasColumnName("menu_item_id");
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(MenuItem.NameMaxLength).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
        builder.Property(i => i.Quantity).HasColumnName("quantity");
        builder.Ignore(i => i.LineTotal);

        builder.HasOne<MenuItem>()
            .WithMany()
            .HasForeignKey(i => i.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => new { i.OrderId, i.MenuItemId }).IsUnique();
    }
}
