using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations;

public partial class SeedInventoryFeedStagesManage : Migration
{
    private static readonly Guid PermissionId = Guid.Parse("08000000-0000-0000-0000-000000000001");
    private static readonly Guid RoleAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var now = DateTimeOffset.UtcNow;
        migrationBuilder.InsertData("permissions", new[] { "id", "code", "name", "module", "description", "created_at" },
            new object[,] { { PermissionId, "inventory.feed-stages.manage", "Gestionar Etapas de Alimento", "Inventory", "Permite crear y activar o desactivar etapas de alimento", now } }, schema: "people");
        migrationBuilder.InsertData("role_permissions", new[] { "role_id", "permission_id" },
            new object[,] { { RoleAdminId, PermissionId } }, schema: "people");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id = '08000000-0000-0000-0000-000000000001'::uuid;");
        migrationBuilder.Sql("DELETE FROM people.permissions WHERE id = '08000000-0000-0000-0000-000000000001'::uuid;");
    }
}
