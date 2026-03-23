using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class UserFilterPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookingItems_ServiceItems_ServiceItemId",
                table: "CrmResourceBookingItems");

            migrationBuilder.CreateTable(
                name: "UserFilterPresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ViewCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FiltersJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFilterPresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFilterPresets_Employees_UserId",
                        column: x => x.UserId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode",
                table: "UserFilterPresets",
                columns: new[] { "UserId", "EntityCode", "ViewCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode_Name",
                table: "UserFilterPresets",
                columns: new[] { "UserId", "EntityCode", "ViewCode", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookingItems_ServiceItems_ServiceItemId",
                table: "CrmResourceBookingItems",
                column: "ServiceItemId",
                principalTable: "ServiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookingItems_ServiceItems_ServiceItemId",
                table: "CrmResourceBookingItems");

            migrationBuilder.DropTable(
                name: "UserFilterPresets");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookingItems_ServiceItems_ServiceItemId",
                table: "CrmResourceBookingItems",
                column: "ServiceItemId",
                principalTable: "ServiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
