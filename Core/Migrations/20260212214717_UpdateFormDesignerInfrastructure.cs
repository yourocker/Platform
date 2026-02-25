using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class UpdateFormDesignerInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "AppFormDefinitions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions",
                columns: new[] { "AppDefinitionId", "Type", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "AppFormDefinitions");
        }
    }
}
