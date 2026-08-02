using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Inventory.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordedBy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "recorded_by",
                schema: "inventory",
                table: "group_feed_consumptions",
                newName: "recorded_by_label");

            migrationBuilder.AddColumn<Guid>(
                name: "recorded_by_id",
                schema: "inventory",
                table: "group_feed_consumptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "i_x_group_feed_consumptions_recorded_by_id",
                schema: "inventory",
                table: "group_feed_consumptions",
                column: "recorded_by_id");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT FROM information_schema.tables 
                        WHERE table_schema = 'people' AND table_name = 'users'
                    ) THEN
                        UPDATE inventory.group_feed_consumptions c
                        SET recorded_by_id = u.id
                        FROM people.users u
                        WHERE LOWER(u.full_name) = LOWER(c.recorded_by_label)
                           OR LOWER(u.email) = LOWER(c.recorded_by_label);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_group_feed_consumptions_recorded_by_id",
                schema: "inventory",
                table: "group_feed_consumptions");

            migrationBuilder.DropColumn(
                name: "recorded_by_id",
                schema: "inventory",
                table: "group_feed_consumptions");

            migrationBuilder.RenameColumn(
                name: "recorded_by_label",
                schema: "inventory",
                table: "group_feed_consumptions",
                newName: "recorded_by");
        }
    }
}
