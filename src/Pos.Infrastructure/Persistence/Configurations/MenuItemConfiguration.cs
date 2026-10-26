using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Configurations;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.Name).HasColumnName("name").IsRequired();
        builder.Property(m => m.Price).HasColumnName("price")
            .HasConversion<MoneyConverter>().HasColumnType(MoneyConverter.ColumnType);
    }
}
