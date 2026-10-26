using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderNumberStatusCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "orders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<int>(
                name: "number",
                table: "orders",
                type: "integer",
                nullable: false)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            // The defaults only fill in orders that existed before this migration; new orders always set both.
            migrationBuilder.Sql("ALTER TABLE orders ALTER COLUMN created_at DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE orders ALTER COLUMN status DROP DEFAULT;");

            migrationBuilder.CreateIndex(
                name: "IX_orders_created_at",
                table: "orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_orders_number",
                table: "orders",
                column: "number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_created_at",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "IX_orders_number",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "number",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "status",
                table: "orders");
        }
    }
}
