using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seeds the Delivery module permissions (ADR-0035 / spec 0014):
    /// - delivery.builds.manage (admin)
    /// - delivery.releases.publish (admin)
    /// - delivery.releases.download (admin, registrar, veterinarian)
    /// </summary>
    public partial class SeedDeliveryPermissions : Migration
    {
        private static readonly Guid PermDeliveryBuildsManage =
            Guid.Parse("14000000-0000-0000-0000-000000000001");

        private static readonly Guid PermDeliveryReleasesPublish =
            Guid.Parse("14000000-0000-0000-0000-000000000002");

        private static readonly Guid PermDeliveryReleasesDownload =
            Guid.Parse("14000000-0000-0000-0000-000000000003");

        private static readonly Guid RoleAdminId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static readonly Guid RoleRegistrarId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly Guid RoleVeterinarianId =
            Guid.Parse("33333333-3333-3333-3333-333333333333");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module", "description", "created_at" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(100)", "character varying(50)", "character varying(500)", "timestamp with time zone" },
                values: new object[,]
                {
                    { PermDeliveryBuildsManage, "delivery.builds.manage", "Gestionar compilaciones móviles", "Delivery", "Permite solicitar builds Android y consultar su estado y detalle de ejecución", now },
                    { PermDeliveryReleasesPublish, "delivery.releases.publish", "Publicar releases móviles", "Delivery", "Permite publicar releases candidatas a producción o retirar versiones de forma auditada", now },
                    { PermDeliveryReleasesDownload, "delivery.releases.download", "Descargar releases móviles", "Delivery", "Permite consultar el catálogo y descargar binarios APK de releases móviles publicadas", now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "role_id", "permission_id" },
                columnTypes: new[] { "uuid", "uuid" },
                values: new object[,]
                {
                    { RoleAdminId, PermDeliveryBuildsManage },
                    { RoleAdminId, PermDeliveryReleasesPublish },
                    { RoleAdminId, PermDeliveryReleasesDownload },
                    { RoleRegistrarId, PermDeliveryReleasesDownload },
                    { RoleVeterinarianId, PermDeliveryReleasesDownload },
                },
                schema: "people");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id IN ('14000000-0000-0000-0000-000000000001'::uuid, '14000000-0000-0000-0000-000000000002'::uuid, '14000000-0000-0000-0000-000000000003'::uuid);");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id IN ('14000000-0000-0000-0000-000000000001'::uuid, '14000000-0000-0000-0000-000000000002'::uuid, '14000000-0000-0000-0000-000000000003'::uuid);");
        }
    }
}
