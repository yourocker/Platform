using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class BookingModuleAndFeatureToggles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_ResourceId",
                table: "CrmResourceBookings");

            migrationBuilder.AddColumn<bool>(
                name: "AllowOverbooking",
                table: "CrmResources",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxParallelBookings",
                table: "CrmResources",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "CrmResourceBookings",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "BookingPolicySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AllowOverbooking = table.Column<bool>(type: "boolean", nullable: false),
                    MaxParallelBookings = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingPolicySettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FeatureToggles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FeatureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureToggles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_CreatedAt",
                table: "CrmResourceBookings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_ResourceId_StartTime_EndTime",
                table: "CrmResourceBookings",
                columns: new[] { "ResourceId", "StartTime", "EndTime" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureToggles_FeatureCode",
                table: "FeatureToggles",
                column: "FeatureCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BookingPolicySettings");

            migrationBuilder.DropTable(
                name: "FeatureToggles");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_CreatedAt",
                table: "CrmResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_ResourceId_StartTime_EndTime",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "AllowOverbooking",
                table: "CrmResources");

            migrationBuilder.DropColumn(
                name: "MaxParallelBookings",
                table: "CrmResources");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "CrmResourceBookings");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_ResourceId",
                table: "CrmResourceBookings",
                column: "ResourceId");
        }
    }
}
