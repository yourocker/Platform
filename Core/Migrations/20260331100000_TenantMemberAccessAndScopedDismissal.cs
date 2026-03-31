using System;
using Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260331100000_TenantMemberAccessAndScopedDismissal")]
    public partial class TenantMemberAccessAndScopedDismissal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DismissedAt",
                table: "EmployeeTenantMemberships",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDismissed",
                table: "EmployeeTenantMemberships",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE "EmployeeTenantMemberships" AS m
                SET
                    "IsDismissed" = e."IsDismissed",
                    "DismissedAt" = CASE
                        WHEN e."IsDismissed" THEN COALESCE(m."DismissedAt", NOW() AT TIME ZONE 'UTC')
                        ELSE NULL
                    END,
                    "IsActive" = CASE
                        WHEN e."IsDismissed" AND NOT m."IsActive" THEN TRUE
                        ELSE m."IsActive"
                    END
                FROM "Employees" AS e
                WHERE e."Id" = m."EmployeeId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DismissedAt",
                table: "EmployeeTenantMemberships");

            migrationBuilder.DropColumn(
                name: "IsDismissed",
                table: "EmployeeTenantMemberships");
        }
    }
}
