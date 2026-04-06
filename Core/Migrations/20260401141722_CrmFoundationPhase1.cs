using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class CrmFoundationPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConvertedDealId",
                table: "CrmLeads");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConvertedAt",
                table: "CrmLeads",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyId",
                table: "CrmDeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmCompanies_GenericObjects_Id",
                        column: x => x.Id,
                        principalTable: "GenericObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmDealContacts",
                columns: table => new
                {
                    DealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmDealContacts", x => new { x.DealId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_CrmDealContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmDealContacts_CrmDeals_DealId",
                        column: x => x.DealId,
                        principalTable: "CrmDeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmEntityRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntityCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntityCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmEntityRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmEntityRelations_Employees_CreatedByEmployeeId",
                        column: x => x.CreatedByEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CrmSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseLeads = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmCompanyContacts",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmCompanyContacts", x => new { x.CompanyId, x.ContactId });
                    table.ForeignKey(
                        name: "FK_CrmCompanyContacts_Contacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "Contacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmCompanyContacts_CrmCompanies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "CrmCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmDeals_CompanyId",
                table: "CrmDeals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmDeals_SourceLeadId",
                table: "CrmDeals",
                column: "SourceLeadId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCompanyContacts_ContactId",
                table: "CrmCompanyContacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmCompanyContacts_TenantId_ContactId",
                table: "CrmCompanyContacts",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmDealContacts_ContactId",
                table: "CrmDealContacts",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmDealContacts_TenantId_ContactId",
                table: "CrmDealContacts",
                columns: new[] { "TenantId", "ContactId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmEntityRelations_CreatedByEmployeeId",
                table: "CrmEntityRelations",
                column: "CreatedByEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmEntityRelations_TenantId_SourceEntityCode_SourceEntityId~",
                table: "CrmEntityRelations",
                columns: new[] { "TenantId", "SourceEntityCode", "SourceEntityId", "RelationType" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmEntityRelations_TenantId_TargetEntityCode_TargetEntityId~",
                table: "CrmEntityRelations",
                columns: new[] { "TenantId", "TargetEntityCode", "TargetEntityId", "RelationType" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmSettings_TenantId",
                table: "CrmSettings",
                column: "TenantId",
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "CrmSettings" ("Id", "TenantId", "UseLeads", "UpdatedAt")
                SELECT t."Id", t."Id", TRUE, NOW()
                FROM "Tenants" t
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM "CrmSettings" s
                    WHERE s."TenantId" = t."Id"
                );
                """);

            migrationBuilder.Sql("""
                UPDATE "CrmLeads"
                SET "ConvertedAt" = COALESCE("ConvertedAt", "StageChangedAt")
                WHERE "IsConverted" = TRUE;
                """);

            migrationBuilder.Sql("""
                INSERT INTO "CrmDealContacts" ("DealId", "ContactId", "TenantId", "IsPrimary")
                SELECT d."Id", d."ContactId", g."TenantId", TRUE
                FROM "CrmDeals" d
                INNER JOIN "GenericObjects" g ON g."Id" = d."Id"
                WHERE d."ContactId" IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM "CrmDealContacts" dc
                      WHERE dc."DealId" = d."Id"
                        AND dc."ContactId" = d."ContactId"
                  );
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmDeals_CrmCompanies_CompanyId",
                table: "CrmDeals",
                column: "CompanyId",
                principalTable: "CrmCompanies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_CrmDeals_CrmLeads_SourceLeadId",
                table: "CrmDeals",
                column: "SourceLeadId",
                principalTable: "CrmLeads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmDeals_CrmCompanies_CompanyId",
                table: "CrmDeals");

            migrationBuilder.DropForeignKey(
                name: "FK_CrmDeals_CrmLeads_SourceLeadId",
                table: "CrmDeals");

            migrationBuilder.DropTable(
                name: "CrmCompanyContacts");

            migrationBuilder.DropTable(
                name: "CrmDealContacts");

            migrationBuilder.DropTable(
                name: "CrmEntityRelations");

            migrationBuilder.DropTable(
                name: "CrmSettings");

            migrationBuilder.DropTable(
                name: "CrmCompanies");

            migrationBuilder.DropIndex(
                name: "IX_CrmDeals_CompanyId",
                table: "CrmDeals");

            migrationBuilder.DropIndex(
                name: "IX_CrmDeals_SourceLeadId",
                table: "CrmDeals");

            migrationBuilder.DropColumn(
                name: "ConvertedAt",
                table: "CrmLeads");

            migrationBuilder.DropColumn(
                name: "CompanyId",
                table: "CrmDeals");

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedDealId",
                table: "CrmLeads",
                type: "uuid",
                nullable: true);
        }
    }
}
