using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedicalBot.Migrations
{
    /// <inheritdoc />
    public partial class FixTaskRelationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedEntityCode",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "GenericObjects");

            migrationBuilder.CreateTable(
                name: "TaskEntityRelations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskEntityRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskEntityRelations_GenericObjects_TaskId",
                        column: x => x.TaskId,
                        principalTable: "GenericObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskEntityRelations_TaskId",
                table: "TaskEntityRelations",
                column: "TaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TaskEntityRelations");

            migrationBuilder.AddColumn<string>(
                name: "RelatedEntityCode",
                table: "GenericObjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RelatedEntityId",
                table: "GenericObjects",
                type: "uuid",
                nullable: true);
        }
    }
}
