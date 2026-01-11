using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBot.Migrations
{
    /// <inheritdoc />
    public partial class FixFieldDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SettingsJson",
                table: "AppFieldDefinitions");

            migrationBuilder.AddColumn<bool>(
                name: "IsArray",
                table: "AppFieldDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetEntityCode",
                table: "AppFieldDefinitions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArray",
                table: "AppFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "TargetEntityCode",
                table: "AppFieldDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "SettingsJson",
                table: "AppFieldDefinitions",
                type: "jsonb",
                nullable: true);
        }
    }
}
