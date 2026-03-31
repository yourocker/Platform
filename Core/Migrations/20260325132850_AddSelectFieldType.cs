using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Migrations
{
    /// <inheritdoc />
    public partial class AddSelectFieldType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AppFieldDefinitions'
                          AND column_name = 'OptionsJson'
                    ) THEN
                        ALTER TABLE "AppFieldDefinitions" ADD "OptionsJson" text;
                    END IF;
                END
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_schema = 'public'
                          AND table_name = 'AppFieldDefinitions'
                          AND column_name = 'OptionsJson'
                    ) THEN
                        ALTER TABLE "AppFieldDefinitions" DROP COLUMN "OptionsJson";
                    END IF;
                END
                $$;
                """);
        }
    }
}
