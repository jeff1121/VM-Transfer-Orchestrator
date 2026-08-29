using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VMTO.Infrastructure.Persistence;

#nullable disable

namespace VMTO.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260828140000_Phase13aPlanAndMetadata")]
public partial class Phase13aPlanAndMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "metadata_json",
            table: "connections",
            type: "jsonb",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "vm_id",
            table: "jobs",
            type: "character varying(256)",
            maxLength: 256,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "plan",
            table: "jobs",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "step_type",
            table: "job_steps",
            type: "text",
            nullable: false,
            defaultValue: "ExportDisk");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "metadata_json", table: "connections");
        migrationBuilder.DropColumn(name: "vm_id", table: "jobs");
        migrationBuilder.DropColumn(name: "plan", table: "jobs");
        migrationBuilder.DropColumn(name: "step_type", table: "job_steps");
    }
}
