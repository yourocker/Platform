using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueFieldSystemName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId",
                table: "AppFieldDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions",
                columns: new[] { "AppDefinitionId", "SystemName" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId",
                table: "AppFieldDefinitions",
                column: "AppDefinitionId");
        }
    }
}
