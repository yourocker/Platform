using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class BookingStatusesAndContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                table: "CrmResourceBookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookingStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmResourceBookingContacts",
                columns: table => new
                {
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmResourceBookingContacts", x => new { x.BookingId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_CrmResourceBookingContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmResourceBookingContacts_CrmResourceBookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "CrmResourceBookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookings_StatusId",
                table: "CrmResourceBookings",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatuses_Category",
                table: "BookingStatuses",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatuses_IsActive",
                table: "BookingStatuses",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_CrmResourceBookingContacts_ContactId",
                table: "CrmResourceBookingContacts",
                column: "ContactId");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmResourceBookings_BookingStatuses_StatusId",
                table: "CrmResourceBookings",
                column: "StatusId",
                principalTable: "BookingStatuses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmResourceBookings_BookingStatuses_StatusId",
                table: "CrmResourceBookings");

            migrationBuilder.DropTable(
                name: "BookingStatuses");

            migrationBuilder.DropTable(
                name: "CrmResourceBookingContacts");

            migrationBuilder.DropIndex(
                name: "IX_CrmResourceBookings_StatusId",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "CrmResourceBookings");
        }
    }
}
