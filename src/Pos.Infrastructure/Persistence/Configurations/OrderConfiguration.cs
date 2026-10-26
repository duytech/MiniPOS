using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.Orders;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(o => o.Number).HasColumnName("number").UseIdentityByDefaultColumn();
        builder.HasIndex(o => o.Number).IsUnique();
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.HasIndex(o => o.CreatedAt);
        builder.Property(o => o.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3);

        // Postgres system column that changes on every update of the row: the optimistic concurrency token.
        builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.HasMany(o => o.Lines).WithOne()
            .HasForeignKey(OrderLineConfiguration.OrderIdProperty)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
