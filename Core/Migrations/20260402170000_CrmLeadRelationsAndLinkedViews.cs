using System;
using Core.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260402170000_CrmLeadRelationsAndLinkedViews")]
    public partial class CrmLeadRelationsAndLinkedViews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "CrmLeads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmLeadContacts",
                columns: table => new
                {
                    LeadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmLeadContacts", x => new { x.LeadId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_CrmLeadContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmLeadContacts_CrmLeads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "CrmLeads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeads_CompanyId",
                table: "CrmLeads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeadContacts_ContactId",
                table: "CrmLeadContacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmLeadContacts_TenantId_ContactId",
                table: "CrmLeadContacts",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_CrmLeads_ContactId"
                ON "CrmLeads" ("ContactId");
                """);

            migrationBuilder.Sql("""
                INSERT INTO "CrmLeadContacts" ("LeadId", "ContactId", "TenantId", "IsPrimary")
                SELECT l."Id", l."ContactId", g."TenantId", TRUE
                FROM "CrmLeads" l
                INNER JOIN "GenericObjects" g ON g."Id" = l."Id"
                WHERE l."ContactId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CrmLeadContacts" lc
                      WHERE lc."LeadId" = l."Id"
                        AND lc."ContactId" = l."ContactId"
                  );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmLeads_CrmCompanies_CompanyId",
                table: "CrmLeads",
                column: "CompanyId",
                principalTable: "CrmCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmLeads_CrmCompanies_CompanyId",
                table: "CrmLeads");

            migrationBuilder.DropTable(
                name: "CrmLeadContacts");

            migrationBuilder.DropIndex(
                name: "IX_CrmLeads_CompanyId",
                table: "CrmLeads");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CrmLeads");
        }
    }
}
