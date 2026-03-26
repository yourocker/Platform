using System;
using Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260325104500_UserNotificationSourceEventId")]
    public partial class UserNotificationSourceEventId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceEventId",
                table: "UserNotifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_SourceEventId",
                table: "UserNotifications",
                column: "SourceEventId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_SourceEventId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "SourceEventId",
                table: "UserNotifications");
        }
    }
}
