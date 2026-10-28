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
        builder.Property(p => p.Position).HasColumnName("position");
        builder.HasIndex(OrderIdProperty, nameof(SavedSplitPart.Position)).IsUnique();

        builder.HasMany(p => p.Lines).WithOne()
            .HasForeignKey(SavedSplitLineConfiguration.PartIdProperty)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
