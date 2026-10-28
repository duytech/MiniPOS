using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class SavedSplitLineConfiguration : IEntityTypeConfiguration<SavedSplitLine>
{
    public const string PartIdProperty = "PartId";

    public void Configure(EntityTypeBuilder<SavedSplitLine> builder)
    {
        builder.ToTable("order_split_part_lines");
        builder.Property<Guid>(PartIdProperty).HasColumnName("part_id");
        builder.Property(l => l.LineId).HasColumnName("line_id");
        builder.Property(l => l.Position).HasColumnName("position");
        builder.HasKey(PartIdProperty, nameof(SavedSplitLine.LineId));

        builder.HasOne<OrderLine>().WithMany().HasForeignKey(l => l.LineId).OnDelete(DeleteBehavior.Cascade);
    }
}
