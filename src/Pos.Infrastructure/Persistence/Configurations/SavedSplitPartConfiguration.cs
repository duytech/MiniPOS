using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SavedSplitPartConfiguration : IEntityTypeConfiguration<SavedSplitPart>
{
    public const string OrderIdProperty = "OrderId";

    public void Configure(EntityTypeBuilder<SavedSplitPart> builder)
    {
        builder.ToTable("order_split_parts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property<Guid>(OrderIdProperty).HasColumnName("order_id");
        // No unique (order_id, position) index: the order's xmin already rejects a second writer, and the index made
        // racing saves fail on it first (409 with the wrong detail, or a deadlock).
        builder.Property(p => p.Position).HasColumnName("position");

        builder.HasMany(p => p.Items).WithOne()
            .HasForeignKey(SavedSplitMenuItemConfiguration.PartIdProperty)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
