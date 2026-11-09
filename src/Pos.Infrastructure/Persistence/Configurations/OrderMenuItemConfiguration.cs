using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.MenuItems;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class OrderMenuItemConfiguration : IEntityTypeConfiguration<OrderMenuItem>
{
    public const string OrderIdProperty = "OrderId";

    public void Configure(EntityTypeBuilder<OrderMenuItem> builder)
    {
        builder.ToTable("order_menu_items");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>(OrderIdProperty).HasColumnName("order_id");
        builder.Property(l => l.MenuItemId).HasColumnName("menu_item_id");
        builder.Property(l => l.Name).HasColumnName("name").IsRequired();
        builder.Property(l => l.UnitPrice).HasColumnName("unit_price")
            .HasConversion<MoneyConverter>().HasColumnType(MoneyConverter.ColumnType);
        builder.Property(l => l.Quantity).HasColumnName("quantity");
        builder.Property(l => l.Notes).HasColumnName("notes");

        builder.HasOne<MenuItem>().WithMany().HasForeignKey(l => l.MenuItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
