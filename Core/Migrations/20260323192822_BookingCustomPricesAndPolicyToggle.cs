using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class BookingCustomPricesAndPolicyToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CustomUnitPrice",
                table: "CrmResourceBookingItems",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowManualItemPriceChange",
                table: "BookingPolicySettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Historical records stored absolute discount amount by line.
            // Convert them to percentage format used by the new booking editor.
            migrationBuilder.Sql("""
                UPDATE "CrmResourceBookingItems"
                SET "DiscountAmount" = CASE
                    WHEN "UnitPrice" > 0 AND "Quantity" > 0
                        THEN ROUND((("DiscountAmount" / ("UnitPrice" * "Quantity")) * 100.0)::numeric, 1)
                    ELSE 0
                END
                WHERE "DiscountAmount" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomUnitPrice",
                table: "CrmResourceBookingItems");

            migrationBuilder.DropColumn(
                name: "AllowManualItemPriceChange",
                table: "BookingPolicySettings");
        }
    }
}
