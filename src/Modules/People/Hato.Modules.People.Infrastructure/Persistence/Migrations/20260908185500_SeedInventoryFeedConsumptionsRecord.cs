using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the <c>inventory.feed-consumptions.record</c> permission and grants it to the
    /// admin and registrar roles (Feature 0008 Commit 5, D4). Allows registering feed
    /// consumption for animal lots without granting inventory item catalogue management.
    /// </summary>
    public partial class SeedInventoryFeedConsumptionsRecord : Migration
    {
        private static readonly Guid PermInventoryFeedConsumptionsRecord =
            Guid.Parse("08000000-0000-0000-0000-000000000003");

        private static readonly Guid RoleAdminId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid RoleRegistrarId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData(
                "permissions",
                new[] { "id", "code", "name", "module", "description", "created_at" },
                new object[,]
                {
                    { PermInventoryFeedConsumptionsRecord, "inventory.feed-consumptions.record", "Registrar consumos de alimento", "Inventory", "Permite registrar el consumo de alimento de lotes de animales sin otorgar administración del catálogo de inventario", now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                "role_permissions",
                new[] { "role_id", "permission_id" },
                new object[,]
                {
                    { RoleAdminId, PermInventoryFeedConsumptionsRecord },
                    { RoleRegistrarId, PermInventoryFeedConsumptionsRecord },
                },
                schema: "people");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id = '08000000-0000-0000-0000-000000000003'::uuid;");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id = '08000000-0000-0000-0000-000000000003'::uuid;");
        }
    }
}
