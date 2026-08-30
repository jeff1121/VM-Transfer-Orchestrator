using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VMTO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWebhooksAndAddLicenseFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "name",
                table: "job_steps");

            migrationBuilder.CreateTable(
                name: "dead_letter_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_type = table.Column<string>(type: "text", nullable: false),
                    queue_name = table.Column<string>(type: "text", nullable: false),
                    payload = table.Column<string>(type: "text", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    replayed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dead_letter_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_logs_created_at",
                table: "dead_letter_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_dead_letter_logs_status",
                table: "dead_letter_logs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dead_letter_logs");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "job_steps",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
