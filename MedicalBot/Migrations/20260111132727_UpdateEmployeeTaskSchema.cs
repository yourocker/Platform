using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedicalBot.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEmployeeTaskSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskComment_Employees_AuthorId",
                table: "TaskComment");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComment_GenericObjects_TaskId",
                table: "TaskComment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskComment",
                table: "TaskComment");

            migrationBuilder.RenameTable(
                name: "TaskComment",
                newName: "TaskComments");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComment_TaskId",
                table: "TaskComments",
                newName: "IX_TaskComments_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComment_AuthorId",
                table: "TaskComments",
                newName: "IX_TaskComments_AuthorId");

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

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskComments",
                table: "TaskComments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_Employees_AuthorId",
                table: "TaskComments",
                column: "AuthorId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComments_GenericObjects_TaskId",
                table: "TaskComments",
                column: "TaskId",
                principalTable: "GenericObjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_Employees_AuthorId",
                table: "TaskComments");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskComments_GenericObjects_TaskId",
                table: "TaskComments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TaskComments",
                table: "TaskComments");

            migrationBuilder.DropColumn(
                name: "RelatedEntityCode",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "RelatedEntityId",
                table: "GenericObjects");

            migrationBuilder.RenameTable(
                name: "TaskComments",
                newName: "TaskComment");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComments_TaskId",
                table: "TaskComment",
                newName: "IX_TaskComment_TaskId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskComments_AuthorId",
                table: "TaskComment",
                newName: "IX_TaskComment_AuthorId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TaskComment",
                table: "TaskComment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComment_Employees_AuthorId",
                table: "TaskComment",
                column: "AuthorId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskComment_GenericObjects_TaskId",
                table: "TaskComment",
                column: "TaskId",
                principalTable: "GenericObjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
