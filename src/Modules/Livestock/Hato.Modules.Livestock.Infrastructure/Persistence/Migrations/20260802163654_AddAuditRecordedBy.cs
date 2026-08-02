using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Livestock.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "recorded_by",
                schema: "livestock",
                table: "animal_events",
                newName: "recorded_by_label");

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_id",
                schema: "livestock",
                table: "animal_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_animal_events_recorded_by_id",
                schema: "livestock",
                table: "animal_events",
                column: "recorded_by_id");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'people' AND table_name = 'users'
                    ) THEN
                        UPDATE livestock.animal_events e
                        SET recorded_by_id = u.id
                        FROM people.users u
                        WHERE LOWER(u.full_name) = LOWER(e.recorded_by_label)
                           OR LOWER(u.email) = LOWER(e.recorded_by_label);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_animal_events_recorded_by_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.DropColumn(
                name: "recorded_by_id",
                schema: "livestock",
                table: "animal_events");

            migrationBuilder.RenameColumn(
                name: "recorded_by_label",
                schema: "livestock",
                table: "animal_events",
                newName: "recorded_by");
        }
    }
}
