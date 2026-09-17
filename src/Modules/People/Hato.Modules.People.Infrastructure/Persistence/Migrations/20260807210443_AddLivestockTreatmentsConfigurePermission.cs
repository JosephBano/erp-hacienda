using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLivestockTreatmentsConfigurePermission : Migration
    {
        // Permission ID follows the Livestock namespace pattern (b-series).
        private static readonly Guid PermLivestockTreatmentsConfigure = Guid.Parse("b6666666-6666-6666-6666-666666666666");

        private static readonly Guid RoleAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid RoleVeterinarianId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Permission gates CRUD on the administration_routes and
            // treatment_reasons catalogues (docs/spec/plan-0002-fase-3-5/sub-planes/3.5a.2-A.md).
            // Configuring the catalogue is a managerial/veterinary decision,
            // not a registrar's daily work, so it is granted to admin and
            // veterinarian only.
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData(
                "permissions",
                new[] { "id", "code", "name", "module", "description", "created_at" },
                new object[,]
                {
                    { PermLivestockTreatmentsConfigure, "livestock.treatments.configure", "Configurar Catálogos de Tratamientos", "Livestock", "Permite crear y desactivar vías de administración y motivos de tratamiento", now },
                },
                schema: "people");

            migrationBuilder.InsertData(
                "role_permissions",
                new[] { "role_id", "permission_id" },
                new object[,]
                {
                    { RoleAdminId, PermLivestockTreatmentsConfigure },
                    { RoleVeterinarianId, PermLivestockTreatmentsConfigure },
                },
                schema: "people");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM people.role_permissions WHERE permission_id = 'b6666666-6666-6666-6666-666666666666'::uuid;");
            migrationBuilder.Sql("DELETE FROM people.permissions WHERE id = 'b6666666-6666-6666-6666-666666666666'::uuid;");
        }
    }
}