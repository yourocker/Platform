using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedicalBot.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssigneeId",
                table: "GenericObjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AuthorId",
                table: "GenericObjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Deadline",
                table: "GenericObjects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "GenericObjects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "GenericObjects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "GenericObjects",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GenericObjects",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GenericObjects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "GenericObjects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskComment",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskComment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskComment_Employees_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskComment_GenericObjects_TaskId",
                        column: x => x.TaskId,
                        principalTable: "GenericObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GenericObjects_AssigneeId",
                table: "GenericObjects",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_GenericObjects_AuthorId",
                table: "GenericObjects",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComment_AuthorId",
                table: "TaskComment",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskComment_TaskId",
                table: "TaskComment",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_GenericObjects_Employees_AssigneeId",
                table: "GenericObjects",
                column: "AssigneeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GenericObjects_Employees_AuthorId",
                table: "GenericObjects",
                column: "AuthorId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GenericObjects_Employees_AssigneeId",
                table: "GenericObjects");

            migrationBuilder.DropForeignKey(
                name: "FK_GenericObjects_Employees_AuthorId",
                table: "GenericObjects");

            migrationBuilder.DropTable(
                name: "TaskComment");

            migrationBuilder.DropIndex(
                name: "IX_GenericObjects_AssigneeId",
                table: "GenericObjects");

            migrationBuilder.DropIndex(
                name: "IX_GenericObjects_AuthorId",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "AssigneeId",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "AuthorId",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "Deadline",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "GenericObjects");
        }
    }
}
