using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pos.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <remarks>Hand-written as renames (EF scaffolds drop and create, which would lose the stored orders' items).</remarks>
    public partial class RenameOrderLineToOrderMenuItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(name: "order_lines", newName: "order_menu_items");
            migrationBuilder.RenameTable(name: "order_split_part_lines", newName: "order_split_part_menu_items");
            migrationBuilder.RenameColumn(name: "line_id", table: "order_split_part_menu_items", newName: "order_menu_item_id");

            migrationBuilder.RenameIndex(name: "IX_order_lines_menu_item_id", table: "order_menu_items", newName: "IX_order_menu_items_menu_item_id");
            migrationBuilder.RenameIndex(name: "IX_order_lines_order_id", table: "order_menu_items", newName: "IX_order_menu_items_order_id");
            migrationBuilder.RenameIndex(
                name: "IX_order_split_part_lines_line_id",
                table: "order_split_part_menu_items",
                newName: "IX_order_split_part_menu_items_order_menu_item_id");

            RenameConstraint(migrationBuilder, "order_menu_items", "PK_order_lines", "PK_order_menu_items");
            RenameConstraint(migrationBuilder, "order_menu_items", "FK_order_lines_menu_items_menu_item_id", "FK_order_menu_items_menu_items_menu_item_id");
            RenameConstraint(migrationBuilder, "order_menu_items", "FK_order_lines_orders_order_id", "FK_order_menu_items_orders_order_id");
            RenameConstraint(migrationBuilder, "order_split_part_menu_items", "PK_order_split_part_lines", "PK_order_split_part_menu_items");
            RenameConstraint(
                migrationBuilder,
                "order_split_part_menu_items",
                "FK_order_split_part_lines_order_lines_line_id",
                "FK_order_split_part_menu_items_order_menu_items_order_menu_ite~");
            RenameConstraint(
                migrationBuilder,
                "order_split_part_menu_items",
                "FK_order_split_part_lines_order_split_parts_part_id",
                "FK_order_split_part_menu_items_order_split_parts_part_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RenameConstraint(
                migrationBuilder,
                "order_split_part_menu_items",
                "FK_order_split_part_menu_items_order_split_parts_part_id",
                "FK_order_split_part_lines_order_split_parts_part_id");
            RenameConstraint(
                migrationBuilder,
                "order_split_part_menu_items",
                "FK_order_split_part_menu_items_order_menu_items_order_menu_ite~",
                "FK_order_split_part_lines_order_lines_line_id");
            RenameConstraint(migrationBuilder, "order_split_part_menu_items", "PK_order_split_part_menu_items", "PK_order_split_part_lines");
            RenameConstraint(migrationBuilder, "order_menu_items", "FK_order_menu_items_orders_order_id", "FK_order_lines_orders_order_id");
            RenameConstraint(migrationBuilder, "order_menu_items", "FK_order_menu_items_menu_items_menu_item_id", "FK_order_lines_menu_items_menu_item_id");
            RenameConstraint(migrationBuilder, "order_menu_items", "PK_order_menu_items", "PK_order_lines");

            migrationBuilder.RenameIndex(
                name: "IX_order_split_part_menu_items_order_menu_item_id",
                table: "order_split_part_menu_items",
                newName: "IX_order_split_part_lines_line_id");
            migrationBuilder.RenameIndex(name: "IX_order_menu_items_order_id", table: "order_menu_items", newName: "IX_order_lines_order_id");
            migrationBuilder.RenameIndex(name: "IX_order_menu_items_menu_item_id", table: "order_menu_items", newName: "IX_order_lines_menu_item_id");

            migrationBuilder.RenameColumn(name: "order_menu_item_id", table: "order_split_part_menu_items", newName: "line_id");
            migrationBuilder.RenameTable(name: "order_split_part_menu_items", newName: "order_split_part_lines");
            migrationBuilder.RenameTable(name: "order_menu_items", newName: "order_lines");
        }

        private static void RenameConstraint(MigrationBuilder migrationBuilder, string table, string name, string newName) =>
            migrationBuilder.Sql($"""ALTER TABLE "{table}" RENAME CONSTRAINT "{name}" TO "{newName}";""");
    }
}
