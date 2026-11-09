using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SavedSplitMenuItemConfiguration : IEntityTypeConfiguration<SavedSplitMenuItem>
{
    public const string PartIdProperty = "PartId";

    public void Configure(EntityTypeBuilder<SavedSplitMenuItem> builder)
    {
        builder.ToTable("order_split_part_menu_items");
        builder.Property<Guid>(PartIdProperty).HasColumnName("part_id");
        builder.Property(l => l.OrderMenuItemId).HasColumnName("order_menu_item_id");
        builder.Property(l => l.Position).HasColumnName("position");
        builder.HasKey(PartIdProperty, nameof(SavedSplitMenuItem.OrderMenuItemId));

        builder.HasOne<OrderMenuItem>().WithMany().HasForeignKey(l => l.OrderMenuItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
