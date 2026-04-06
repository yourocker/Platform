using System;
using Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260403152000_CrmPipelineCardLayouts")]
    public partial class CrmPipelineCardLayouts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmPipelineCardLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PipelineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Layout = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{\"sections\":[]}'::jsonb"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmPipelineCardLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmPipelineCardLayouts_CrmPipelines_PipelineId",
                        column: x => x.PipelineId,
                        principalTable: "CrmPipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmPipelineCardLayouts_PipelineId",
                table: "CrmPipelineCardLayouts",
                column: "PipelineId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmPipelineCardLayouts_TenantId_PipelineId",
                table: "CrmPipelineCardLayouts",
                columns: new[] { "TenantId", "PipelineId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmPipelineCardLayouts");
        }
    }
}
