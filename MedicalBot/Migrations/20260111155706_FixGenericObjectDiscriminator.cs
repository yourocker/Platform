using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBot.Migrations
{
    /// <inheritdoc />
    public partial class FixGenericObjectDiscriminator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "GenericObjects");

            migrationBuilder.AddColumn<string>(
                name: "ObjectType",
                table: "GenericObjects",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObjectType",
                table: "GenericObjects");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "GenericObjects",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");
        }
    }
}
