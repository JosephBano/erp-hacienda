using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFarmModulesAndSettingsPermissions : Migration
    {
        private static readonly Guid PermSettingsFarmModulesRead = Guid.Parse("07000000-0000-0000-0000-000000000001");
        private static readonly Guid PermSettingsFarmModulesManage = Guid.Parse("07000000-0000-0000-0000-000000000002");

        private static readonly Guid FarmModuleProductionId = Guid.Parse("07000000-0000-0000-0000-0000000000a1");
        private static readonly Guid FarmModuleLivestockId = Guid.Parse("07000000-0000-0000-0000-0000000000a2");
        private static readonly Guid FarmModuleInventoryId = Guid.Parse("07000000-0000-0000-0000-0000000000a3");
        private static readonly Guid FarmModuleBreedingId = Guid.Parse("07000000-0000-0000-0000-0000000000a4");
        private static readonly Guid FarmModuleTasksId = Guid.Parse("07000000-0000-0000-0000-0000000000a5");
        private static readonly Guid FarmModulePeopleId = Guid.Parse("07000000-0000-0000-0000-0000000000a6");

        private static readonly Guid RoleAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid RoleRegistrarId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid RoleVeterinarianId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "farm_modules",
                schema: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    disabled_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_farm_modules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_farm_modules_key",
                schema: "people",
                table: "farm_modules",
                column: "key",
                unique: true);

            // Seed the new permissions (ADR-0019). Read is everything-can-pull;
            // manage is the admin-only write. The same admin role that already
            // owns every other panel gets both, so the seed does not require a
            // second PR to flip the panel switch.
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData(
                "permissions",
                new[] { "id", "code", "name", "module", "description", "created_at" },
                new object[,]
                {
                    { PermSettingsFarmModulesRead, "settings.farm-modules.read", "Ver Módulos del Sistema", "Settings", "Permite leer la configuración de módulos habilitados", now },
                    { PermSettingsFarmModulesManage, "settings.farm-modules.manage", "Gestionar Módulos del Sistema", "Settings", "Permite habilitar y deshabilitar módulos del sistema", now },
                },
                schema: "people");

            // Seed the module toggles. Production ships off for the pilot
            // (the field is a pig farm, not a dairy); the rest stay on until the
            // owner turns them off. The keys here are the
            // ModuleKey union on the field-app's moduleVisibility.ts.
            migrationBuilder.InsertData(
                "farm_modules",
                new[] { "id", "key", "enabled", "disabled_reason", "created_at" },
                new object[,]
                {
                    { FarmModuleProductionId, "production", false, "no aplica al piloto porcino", now },
                    { FarmModuleLivestockId, "livestock", true, null, now },
                    { FarmModuleInventoryId, "inventory", true, null, now },
                    { FarmModuleBreedingId, "breeding", true, null, now },
                    { FarmModuleTasksId, "tasks", true, null, now },
                    { FarmModulePeopleId, "people", true, null, now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                "role_permissions",
                new[] { "role_id", "permission_id" },
                new object[,]
                {
                    // Admin gets both Settings permissions.
                    { RoleAdminId, PermSettingsFarmModulesRead },
                    { RoleAdminId, PermSettingsFarmModulesManage },

                    // Every authenticated employee can read the module list so
                    // the phone can know which modules to show. The manage bit
                    // stays admin-only.
                    { RoleRegistrarId, PermSettingsFarmModulesRead },
                    { RoleVeterinarianId, PermSettingsFarmModulesRead },
                },
                schema: "people");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id IN ('07000000-0000-0000-0000-000000000001'::uuid, '07000000-0000-0000-0000-000000000002'::uuid);");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id IN ('07000000-0000-0000-0000-000000000001'::uuid, '07000000-0000-0000-0000-000000000002'::uuid);");

            migrationBuilder.DropTable(
                name: "farm_modules",
                schema: "people");
        }
    }
}
