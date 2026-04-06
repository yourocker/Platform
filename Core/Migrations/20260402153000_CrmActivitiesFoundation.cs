using System;
using Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260402153000_CrmActivitiesFoundation")]
    public partial class CrmActivitiesFoundation : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmActivities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DueAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmActivities_Employees_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CrmActivities_EmployeeTasks_LinkedTaskId",
                        column: x => x.LinkedTaskId,
                        principalTable: "EmployeeTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrmActivityBindings",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmActivityBindings", x => new { x.ActivityId, x.EntityCode, x.EntityId });
                    table.ForeignKey(
                        name: "FK_CrmActivityBindings_CrmActivities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "CrmActivities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_AuthorId",
                table: "CrmActivities",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_LinkedTaskId",
                table: "CrmActivities",
                column: "LinkedTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_TenantId_CreatedAt",
                table: "CrmActivities",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivities_TenantId_Type_CreatedAt",
                table: "CrmActivities",
                columns: new[] { "TenantId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmActivityBindings_TenantId_EntityCode_EntityId",
                table: "CrmActivityBindings",
                columns: new[] { "TenantId", "EntityCode", "EntityId" });

            migrationBuilder.Sql("""
                INSERT INTO "CrmActivities" (
                    "Id",
                    "TenantId",
                    "Type",
                    "Subject",
                    "Content",
                    "AuthorId",
                    "LinkedTaskId",
                    "CreatedAt",
                    "DueAt",
                    "CompletedAt",
                    "IsPinned"
                )
                SELECT
                    e."Id",
                    e."TenantId",
                    CASE
                        WHEN e."Type" = 4 THEN 1
                        ELSE 0
                    END,
                    e."Title",
                    e."Content",
                    e."EmployeeId",
                    NULL,
                    e."CreatedAt",
                    NULL,
                    NULL,
                    e."IsPinned"
                FROM "CrmEvents" e
                WHERE e."Type" IN (1, 4)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CrmActivities" a
                      WHERE a."Id" = e."Id"
                  );
                """);

            migrationBuilder.Sql("""
                INSERT INTO "CrmActivityBindings" (
                    "ActivityId",
                    "EntityCode",
                    "EntityId",
                    "TenantId",
                    "IsPrimary"
                )
                SELECT
                    e."Id",
                    e."TargetEntityCode",
                    e."TargetId",
                    e."TenantId",
                    TRUE
                FROM "CrmEvents" e
                WHERE e."Type" IN (1, 4)
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CrmActivityBindings" b
                      WHERE b."ActivityId" = e."Id"
                        AND b."EntityCode" = e."TargetEntityCode"
                        AND b."EntityId" = e."TargetId"
                  );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmActivityBindings");

            migrationBuilder.DropTable(
                name: "CrmActivities");
        }
    }
}
