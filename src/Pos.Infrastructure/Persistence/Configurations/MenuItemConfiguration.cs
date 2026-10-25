using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pos.Domain.MenuItems;

namespace Pos.Infrastructure.Persistence.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(MenuItem.NameMaxLength).IsRequired();
        builder.Property(m => m.Price).HasColumnName("price").HasPrecision(12, 2);
        builder.Property(m => m.IsAvailable).HasColumnName("is_available");

        builder.HasData(
            new { Id = Guid.Parse("11111111-1111-1111-1111-000000000001"), Name = "Espresso", Price = 2.50m, IsAvailable = true },
            new { Id = Guid.Parse("11111111-1111-1111-1111-000000000002"), Name = "Cappuccino", Price = 3.75m, IsAvailable = true },
            new { Id = Guid.Parse("11111111-1111-1111-1111-000000000003"), Name = "Croissant", Price = 2.20m, IsAvailable = true },
            new { Id = Guid.Parse("11111111-1111-1111-1111-000000000004"), Name = "Club Sandwich", Price = 7.90m, IsAvailable = true });
    }
}
