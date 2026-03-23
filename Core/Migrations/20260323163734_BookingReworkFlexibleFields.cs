using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class BookingReworkFlexibleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeId",
                table: "CrmResources");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "CrmResources");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByEmployeeId",
                table: "CrmResourceBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PerformerEmployeeId",
                table: "CrmResourceBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Properties",
                table: "CrmResourceBookings",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceItemId",
                table: "CrmResourceBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmResources_IsActive",
                table: "CrmResources",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResources_Name",
                table: "CrmResources",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_CreatedByEmployeeId",
                table: "CrmResourceBookings",
                column: "CreatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_PerformerEmployeeId",
                table: "CrmResourceBookings",
                column: "PerformerEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_ServiceItemId",
                table: "CrmResourceBookings",
                column: "ServiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_StartTime",
                table: "CrmResourceBookings",
                column: "StartTime");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookings_Employees_CreatedByEmployeeId",
                table: "CrmResourceBookings",
                column: "CreatedByEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookings_Employees_PerformerEmployeeId",
                table: "CrmResourceBookings",
                column: "PerformerEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookings_ServiceItems_ServiceItemId",
                table: "CrmResourceBookings",
                column: "ServiceItemId",
                principalTable: "ServiceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookings_Employees_CreatedByEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookings_Employees_PerformerEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookings_ServiceItems_ServiceItemId",
                table: "CrmResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_CrmResources_IsActive",
                table: "CrmResources");

            migrationBuilder.DropIndex(
                name: "IX_CrmResources_Name",
                table: "CrmResources");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_CreatedByEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_PerformerEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_ServiceItemId",
                table: "CrmResourceBookings");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_StartTime",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "PerformerEmployeeId",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "Properties",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "ServiceItemId",
                table: "CrmResourceBookings");

            migrationBuilder.AddColumn<Guid>(
                name: "EmployeeId",
                table: "CrmResources",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "CrmResources",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
