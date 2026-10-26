using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderPricing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "discount_amount",
                table: "orders",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "discount_rate",
                table: "orders",
                type: "numeric(7,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "tax_rate",
                table: "orders",
                type: "numeric(7,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "tip_rate",
                table: "orders",
                type: "numeric(7,4)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "discount_amount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "discount_rate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tax_rate",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tip_rate",
                table: "orders");
        }
    }
}
