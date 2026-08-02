using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Production.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "recorded_by",
                schema: "production",
                table: "milking_sessions",
                newName: "recorded_by_label");

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_id",
                schema: "production",
                table: "milking_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_milking_sessions_recorded_by_id",
                schema: "production",
                table: "milking_sessions",
                column: "recorded_by_id");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'people' AND table_name = 'users'
                    ) THEN
                        UPDATE production.milking_sessions s
                        SET recorded_by_id = u.id
                        FROM people.users u
                        WHERE LOWER(u.full_name) = LOWER(s.recorded_by_label)
                           OR LOWER(u.email) = LOWER(s.recorded_by_label);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_milking_sessions_recorded_by_id",
                schema: "production",
                table: "milking_sessions");

            migrationBuilder.DropColumn(
                name: "recorded_by_id",
                schema: "production",
                table: "milking_sessions");

            migrationBuilder.RenameColumn(
                name: "recorded_by_label",
                schema: "production",
                table: "milking_sessions",
                newName: "recorded_by");
        }
    }
}
