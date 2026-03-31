using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class TenantIsolationAndSecureFileStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_SourceEventId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode",
                table: "UserFilterPresets");

            migrationBuilder.DropIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode_Name",
                table: "UserFilterPresets");

            migrationBuilder.DropIndex(
                name: "IX_UiSettings_EmployeeId",
                table: "UiSettings");

            migrationBuilder.DropIndex(
                name: "IX_FeatureToggles_FeatureCode",
                table: "FeatureToggles");

            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_IsDefault",
                table: "AppFormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserNotifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UserFilterPresets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "UiSettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "StaffAppointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Positions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "OutboxEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "GenericObjects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "FeatureToggles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "EmployeeSchedules",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Employees",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Departments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmStages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmResources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmResourceBookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmPipelines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CrmDealItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CompanyWorkModes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "CompanyHolidays",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BookingStatuses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "BookingPolicySettings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppFormDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppFieldDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppDefinitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "AppCategories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "StoredFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    StoredFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RelativePath = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OwnerEntityCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OwnerEntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    UploadedByEmployeeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            var defaultTenantId = new Guid("11111111-1111-1111-1111-111111111111");

            migrationBuilder.InsertData(
                table: "Tenants",
                columns: new[] { "Id", "Key", "Name", "IsActive", "CreatedAt", "Notes" },
                values: new object[] { defaultTenantId, "default", "Default Company", true, new DateTime(2026, 3, 30, 0, 0, 0, DateTimeKind.Utc), "Seeded by tenant isolation migration." });

            migrationBuilder.Sql($"UPDATE \"UserNotifications\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"UserFilterPresets\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"UiSettings\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"StaffAppointments\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"Positions\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"OutboxEvents\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"GenericObjects\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"FeatureToggles\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"EmployeeSchedules\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"Employees\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"Departments\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmStages\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmResources\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmResourceBookings\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmPipelines\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmEvents\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CrmDealItems\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CompanyWorkModes\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"CompanyHolidays\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"BookingStatuses\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"BookingPolicySettings\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"AppFormDefinitions\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"AppFieldDefinitions\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"AppDefinitions\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");
            migrationBuilder.Sql($"UPDATE \"AppCategories\" SET \"TenantId\" = '{defaultTenantId}' WHERE \"TenantId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_TenantId_SourceEventId",
                table: "UserNotifications",
                columns: new[] { "TenantId", "SourceEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_TenantId_UserId",
                table: "UserNotifications",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_TenantId_UserId_EntityCode_ViewCode",
                table: "UserFilterPresets",
                columns: new[] { "TenantId", "UserId", "EntityCode", "ViewCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_TenantId_UserId_EntityCode_ViewCode_Name",
                table: "UserFilterPresets",
                columns: new[] { "TenantId", "UserId", "EntityCode", "ViewCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_UserId",
                table: "UserFilterPresets",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UiSettings_TenantId_EmployeeId",
                table: "UiSettings",
                columns: new[] { "TenantId", "EmployeeId" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureToggles_TenantId_FeatureCode",
                table: "FeatureToggles",
                columns: new[] { "TenantId", "FeatureCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId",
                table: "AppFormDefinitions",
                column: "AppDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_TenantId_AppDefinitionId_Type_IsDefault",
                table: "AppFormDefinitions",
                columns: new[] { "TenantId", "AppDefinitionId", "Type", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_TenantId_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions",
                columns: new[] { "TenantId", "AppDefinitionId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId",
                table: "AppFieldDefinitions",
                column: "AppDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_AppFieldDefinitions_TenantId_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions",
                columns: new[] { "TenantId", "AppDefinitionId", "SystemName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDefinitions_TenantId_EntityCode",
                table: "AppDefinitions",
                columns: new[] { "TenantId", "EntityCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppCategories_TenantId_Name",
                table: "AppCategories",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_TenantId_OwnerEntityCode_OwnerEntityId",
                table: "StoredFiles",
                columns: new[] { "TenantId", "OwnerEntityCode", "OwnerEntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_StoredFiles_TenantId_RelativePath",
                table: "StoredFiles",
                columns: new[] { "TenantId", "RelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Key",
                table: "Tenants",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredFiles");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_TenantId_SourceEventId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_TenantId_UserId",
                table: "UserNotifications");

            migrationBuilder.DropIndex(
                name: "IX_UserFilterPresets_TenantId_UserId_EntityCode_ViewCode",
                table: "UserFilterPresets");

            migrationBuilder.DropIndex(
                name: "IX_UserFilterPresets_TenantId_UserId_EntityCode_ViewCode_Name",
                table: "UserFilterPresets");

            migrationBuilder.DropIndex(
                name: "IX_UserFilterPresets_UserId",
                table: "UserFilterPresets");

            migrationBuilder.DropIndex(
                name: "IX_UiSettings_TenantId_EmployeeId",
                table: "UiSettings");

            migrationBuilder.DropIndex(
                name: "IX_FeatureToggles_TenantId_FeatureCode",
                table: "FeatureToggles");

            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId",
                table: "AppFormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_TenantId_AppDefinitionId_Type_IsDefault",
                table: "AppFormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFormDefinitions_TenantId_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId",
                table: "AppFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppFieldDefinitions_TenantId_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppDefinitions_TenantId_EntityCode",
                table: "AppDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_AppCategories_TenantId_Name",
                table: "AppCategories");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserNotifications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UserFilterPresets");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "UiSettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StaffAppointments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "OutboxEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GenericObjects");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FeatureToggles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmployeeSchedules");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmStages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmResources");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmResourceBookings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmPipelines");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CrmDealItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CompanyWorkModes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CompanyHolidays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BookingStatuses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BookingPolicySettings");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppFormDefinitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppDefinitions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AppCategories");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_SourceEventId",
                table: "UserNotifications",
                column: "SourceEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_UserId",
                table: "UserNotifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode",
                table: "UserFilterPresets",
                columns: new[] { "UserId", "EntityCode", "ViewCode" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFilterPresets_UserId_EntityCode_ViewCode_Name",
                table: "UserFilterPresets",
                columns: new[] { "UserId", "EntityCode", "ViewCode", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UiSettings_EmployeeId",
                table: "UiSettings",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_FeatureToggles_FeatureCode",
                table: "FeatureToggles",
                column: "FeatureCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_IsDefault",
                table: "AppFormDefinitions",
                columns: new[] { "AppDefinitionId", "Type", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_AppFormDefinitions_AppDefinitionId_Type_Name",
                table: "AppFormDefinitions",
                columns: new[] { "AppDefinitionId", "Type", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppFieldDefinitions_AppDefinitionId_SystemName",
                table: "AppFieldDefinitions",
                columns: new[] { "AppDefinitionId", "SystemName" },
                unique: true);
        }
    }
}
