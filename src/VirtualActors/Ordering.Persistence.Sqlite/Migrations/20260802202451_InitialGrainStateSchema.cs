using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Persistence.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialGrainStateSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GrainStates",
                columns: table => new
                {
                    ServiceId = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    ProviderName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    StateName = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GrainType = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    GrainId = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    Payload = table.Column<byte[]>(type: "BLOB", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false),
                    ModifiedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrainStates", x => new { x.ServiceId, x.ProviderName, x.StateName, x.GrainType, x.GrainId });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GrainStates");
        }
    }
}
