using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hato.Modules.Delivery.Infrastructure.Persistence.Migrations
{
    public partial class InitialDelivery : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.CreateTable(
                name: "mobile_build_requests",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_code = table.Column<int>(type: "integer", nullable: false),
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_api_url = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    workflow_run_id = table.Column<long>(type: "bigint", nullable: true),
                    workflow_run_attempt = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    group_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mobile_build_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_releases",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    build_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version_code = table.Column<int>(type: "integer", nullable: false),
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_api_url = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    commit_sha = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    release_tag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    artifact_file_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    artifact_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    artifact_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    signing_certificate_fingerprint = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_current_stable = table.Column<bool>(type: "boolean", nullable: false),
                    is_last_good = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    api_compatibility = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_by = table.Column<Guid>(type: "uuid", nullable: true),
                    withdraw_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mobile_releases", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mobile_release_transition_audits",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mobile_release_transition_audits", x => x.id);
                    table.ForeignKey(
                        name: "f_k_mobile_release_transition_audits_mobile_releases_release_id",
                        column: x => x.release_id,
                        principalSchema: "delivery",
                        principalTable: "mobile_releases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mobile_version_code_sequences",
                schema: "delivery",
                columns: table => new
                {
                    package_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_version_code = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("p_k_mobile_version_code_sequences", x => x.package_name);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_build_requests_channel_idempotency_key",
                schema: "delivery",
                table: "mobile_build_requests",
                columns: new[] { "channel", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_build_requests_created_at",
                schema: "delivery",
                table: "mobile_build_requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_build_requests_status",
                schema: "delivery",
                table: "mobile_build_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_releases_channel",
                schema: "delivery",
                table: "mobile_releases",
                column: "channel",
                unique: true,
                filter: "is_current_stable = true");

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_releases_channel_status",
                schema: "delivery",
                table: "mobile_releases",
                columns: new[] { "channel", "status" });

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_releases_created_at",
                schema: "delivery",
                table: "mobile_releases",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_releases_package_name_version_code",
                schema: "delivery",
                table: "mobile_releases",
                columns: new[] { "package_name", "version_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_release_transition_audits_changed_at",
                schema: "delivery",
                table: "mobile_release_transition_audits",
                column: "changed_at");

            migrationBuilder.CreateIndex(
                name: "i_x_mobile_release_transition_audits_release_id",
                schema: "delivery",
                table: "mobile_release_transition_audits",
                column: "release_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_build_requests",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "mobile_release_transition_audits",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "mobile_releases",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "mobile_version_code_sequences",
                schema: "delivery");
        }
    }
}
