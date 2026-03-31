using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class TenantMembershipsAndSoftDeleteTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "GenericObjects",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "GenericObjects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "CrmResourceBookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "CrmResourceBookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmployeeTenantMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeTenantMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeTenantMemberships_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeTenantMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTenantMemberships_EmployeeId_IsActive",
                table: "EmployeeTenantMemberships",
                columns: new[] { "EmployeeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeTenantMemberships_TenantId_EmployeeId",
                table: "EmployeeTenantMemberships",
                columns: new[] { "TenantId", "EmployeeId" },
                unique: true);

            migrationBuilder.Sql("""
                UPDATE "GenericObjects" AS go
                SET
                    "IsDeleted" = et."IsDeleted",
                    "DeletedAt" = et."DeletedAt"
                FROM "EmployeeTasks" AS et
                WHERE go."Id" = et."Id";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "EmployeeTenantMemberships" (
                    "Id",
                    "TenantId",
                    "EmployeeId",
                    "RoleCode",
                    "IsActive",
                    "IsDefault",
                    "JoinedAt"
                )
                SELECT
                    e."Id",
                    e."TenantId",
                    e."Id",
                    'employee',
                    NOT e."IsDismissed",
                    TRUE,
                    NOW() AT TIME ZONE 'UTC'
                FROM "Employees" AS e
                WHERE e."TenantId" <> '00000000-0000-0000-0000-000000000000'
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "EmployeeTenantMemberships" AS m
                      WHERE m."TenantId" = e."TenantId"
                        AND m."EmployeeId" = e."Id"
                  );
                """);

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "EmployeeTasks");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "EmployeeTasks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeTenantMemberships");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "CrmResourceBookings");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "EmployeeTasks",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "EmployeeTasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "EmployeeTasks" AS et
                SET
                    "IsDeleted" = go."IsDeleted",
                    "DeletedAt" = go."DeletedAt"
                FROM "GenericObjects" AS go
                WHERE go."Id" = et."Id";
                """);
        }
    }
}
