using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VMTO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "artifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    checksum_algorithm = table.Column<string>(type: "text", nullable: false),
                    checksum_value = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    storage_uri = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    entity_type = table.Column<string>(type: "text", nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<string>(type: "text", nullable: true),
                    details = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    endpoint = table.Column<string>(type: "text", nullable: false),
                    cipher_text = table.Column<string>(type: "text", nullable: false),
                    key_id = table.Column<string>(type: "text", nullable: true),
                    validated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_id = table.Column<string>(type: "text", nullable: false),
                    source_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_connection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_type = table.Column<string>(type: "text", nullable: false),
                    storage_endpoint = table.Column<string>(type: "text", nullable: false),
                    storage_bucket_or_path = table.Column<string>(type: "text", nullable: false),
                    storage_region = table.Column<string>(type: "text", nullable: true),
                    strategy = table.Column<string>(type: "text", nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "licenses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    plan = table.Column<string>(type: "text", nullable: false),
                    features = table.Column<string>(type: "jsonb", nullable: false),
                    max_concurrent_jobs = table.Column<int>(type: "integer", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    activation_bindings = table.Column<string>(type: "jsonb", nullable: false),
                    signature = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_licenses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    retry_count = table.Column<int>(type: "integer", nullable: false),
                    max_retries = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    logs_uri = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_steps", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_steps_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_artifacts_job_id",
                table: "artifacts",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_steps_job_id",
                table: "job_steps",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_licenses_key",
                table: "licenses",
                column: "key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artifacts");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "connections");

            migrationBuilder.DropTable(
                name: "job_steps");

            migrationBuilder.DropTable(
                name: "licenses");

            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}
