using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.People.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRbacPermissions : Migration
    {
        private static readonly Guid RoleAdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid RoleRegistrarId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid RoleVeterinarianId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private static readonly Guid PermPeopleUsersManage = Guid.Parse("a1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermPeopleRolesManage = Guid.Parse("a2222222-2222-2222-2222-222222222222");
        private static readonly Guid PermPeopleUsersRead = Guid.Parse("a3333333-3333-3333-3333-333333333333");

        private static readonly Guid PermLivestockAnimalsRead = Guid.Parse("b1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermLivestockAnimalsWrite = Guid.Parse("b2222222-2222-2222-2222-222222222222");
        private static readonly Guid PermLivestockCategoriesManage = Guid.Parse("b3333333-3333-3333-3333-333333333333");
        private static readonly Guid PermLivestockBreedsManage = Guid.Parse("b4444444-4444-4444-4444-444444444444");
        private static readonly Guid PermLivestockSpeciesManage = Guid.Parse("b5555555-5555-5555-5555-555555555555");

        private static readonly Guid PermProductionMilkingRecord = Guid.Parse("c1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermProductionMilkingRead = Guid.Parse("c2222222-2222-2222-2222-222222222222");

        private static readonly Guid PermInventoryItemsManage = Guid.Parse("d1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermInventoryItemsRead = Guid.Parse("d2222222-2222-2222-2222-222222222222");

        private static readonly Guid PermBreedingEventsRecord = Guid.Parse("e1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermBreedingEventsRead = Guid.Parse("e2222222-2222-2222-2222-222222222222");

        private static readonly Guid PermTasksManage = Guid.Parse("f1111111-1111-1111-1111-111111111111");
        private static readonly Guid PermTasksRead = Guid.Parse("f2222222-2222-2222-2222-222222222222");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "people",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "f_k_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "people",
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "people",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                schema: "people",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "f_k_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "people",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "f_k_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "people",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_permissions_code",
                schema: "people",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_role_permissions_permission_id",
                schema: "people",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "i_x_roles_code",
                schema: "people",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_user_roles_role_id",
                schema: "people",
                table: "user_roles",
                column: "role_id");

            // Seed Permissions
            var now = DateTimeOffset.UtcNow;
            migrationBuilder.InsertData("permissions", new[] { "id", "code", "name", "module", "description", "created_at" }, new object[,]
            {
                { PermPeopleUsersManage, "people.users.manage", "Gestionar Usuarios", "People", "Permite crear y desactivar usuarios", now },
                { PermPeopleRolesManage, "people.roles.manage", "Gestionar Roles y Permisos", "People", "Permite definir y asignar roles y permisos", now },
                { PermPeopleUsersRead, "people.users.read", "Ver Usuarios", "People", "Permite consultar usuarios", now },
                { PermLivestockAnimalsRead, "livestock.animals.read", "Ver Animales", "Livestock", "Permite consultar inventarios de ganado", now },
                { PermLivestockAnimalsWrite, "livestock.animals.write", "Registrar Animales", "Livestock", "Permite crear y modificar animales", now },
                { PermLivestockCategoriesManage, "livestock.categories.manage", "Gestionar Categorías", "Livestock", "Permite definir categorías de animales", now },
                { PermLivestockBreedsManage, "livestock.breeds.manage", "Gestionar Razas", "Livestock", "Permite definir razas de animales", now },
                { PermLivestockSpeciesManage, "livestock.species.manage", "Gestionar Especies", "Livestock", "Permite definir especies", now },
                { PermProductionMilkingRecord, "production.milking.record", "Registrar Ordeño", "Production", "Permite registrar producción de leche", now },
                { PermProductionMilkingRead, "production.milking.read", "Ver Ordeño", "Production", "Permite consultar registros de producción", now },
                { PermInventoryItemsManage, "inventory.items.manage", "Gestionar Inventario", "Inventory", "Permite registrar medicamentos e insumos", now },
                { PermInventoryItemsRead, "inventory.items.read", "Ver Inventario", "Inventory", "Permite consultar stock", now },
                { PermBreedingEventsRecord, "breeding.events.record", "Registrar Eventos Reproductivos", "Breeding", "Permite registrar montas, inseminaciones y partos", now },
                { PermBreedingEventsRead, "breeding.events.read", "Ver Reproducción", "Breeding", "Permite consultar historial reproductivo", now },
                { PermTasksManage, "tasks.manage", "Gestionar Tareas", "Tasks", "Permite asignar tareas de hacienda", now },
                { PermTasksRead, "tasks.read", "Ver Tareas", "Tasks", "Permite consultar tareas asignadas", now }
            }, schema: "people");

            // Seed Roles
            migrationBuilder.InsertData("roles", new[] { "id", "code", "name", "description", "is_system", "created_at" }, new object[,]
            {
                { RoleAdminId, "admin", "Administrador", "Acceso total de administración", true, now },
                { RoleRegistrarId, "registrar", "Registrador", "Operador de campo para registro diario", true, now },
                { RoleVeterinarianId, "veterinarian", "Veterinario", "Especialista en salud y reproducción", true, now }
            }, schema: "people");

            // Seed Role Permissions
            migrationBuilder.InsertData("role_permissions", new[] { "role_id", "permission_id" }, new object[,]
            {
                // Admin gets all permissions
                { RoleAdminId, PermPeopleUsersManage },
                { RoleAdminId, PermPeopleRolesManage },
                { RoleAdminId, PermPeopleUsersRead },
                { RoleAdminId, PermLivestockAnimalsRead },
                { RoleAdminId, PermLivestockAnimalsWrite },
                { RoleAdminId, PermLivestockCategoriesManage },
                { RoleAdminId, PermLivestockBreedsManage },
                { RoleAdminId, PermLivestockSpeciesManage },
                { RoleAdminId, PermProductionMilkingRecord },
                { RoleAdminId, PermProductionMilkingRead },
                { RoleAdminId, PermInventoryItemsManage },
                { RoleAdminId, PermInventoryItemsRead },
                { RoleAdminId, PermBreedingEventsRecord },
                { RoleAdminId, PermBreedingEventsRead },
                { RoleAdminId, PermTasksManage },
                { RoleAdminId, PermTasksRead },

                // Registrar permissions
                { RoleRegistrarId, PermLivestockAnimalsRead },
                { RoleRegistrarId, PermLivestockAnimalsWrite },
                { RoleRegistrarId, PermProductionMilkingRecord },
                { RoleRegistrarId, PermProductionMilkingRead },
                { RoleRegistrarId, PermInventoryItemsRead },
                { RoleRegistrarId, PermBreedingEventsRecord },
                { RoleRegistrarId, PermBreedingEventsRead },
                { RoleRegistrarId, PermTasksRead },

                // Veterinarian permissions
                { RoleVeterinarianId, PermLivestockAnimalsRead },
                { RoleVeterinarianId, PermLivestockAnimalsWrite },
                { RoleVeterinarianId, PermInventoryItemsManage },
                { RoleVeterinarianId, PermInventoryItemsRead },
                { RoleVeterinarianId, PermBreedingEventsRecord },
                { RoleVeterinarianId, PermBreedingEventsRead },
                { RoleVeterinarianId, PermTasksManage },
                { RoleVeterinarianId, PermTasksRead }
            }, schema: "people");

            // Backfill existing users: Map legacy string 'role' column to user_roles
            migrationBuilder.Sql(@"
                INSERT INTO people.user_roles (user_id, role_id)
                SELECT u.id,
                       CASE
                           WHEN LOWER(u.role) = 'admin' THEN '11111111-1111-1111-1111-111111111111'::uuid
                           WHEN LOWER(u.role) = 'veterinarian' THEN '33333333-3333-3333-3333-333333333333'::uuid
                           ELSE '22222222-2222-2222-2222-222222222222'::uuid
                       END
                FROM people.users u
                ON CONFLICT DO NOTHING;
            ");

            // Now drop legacy 'role' column safely
            migrationBuilder.DropColumn(
                name: "role",
                schema: "people",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                schema: "people",
                table: "users",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Registrar");

            migrationBuilder.Sql(@"
                UPDATE people.users u
                SET role = COALESCE((
                    SELECT r.code
                    FROM people.user_roles ur
                    JOIN people.roles r ON r.id = ur.role_id
                    WHERE ur.user_id = u.id
                    LIMIT 1
                ), 'registrar');
            ");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "people");

            migrationBuilder.DropTable(
                name: "user_roles",
                schema: "people");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "people");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "people");
        }
    }
}
