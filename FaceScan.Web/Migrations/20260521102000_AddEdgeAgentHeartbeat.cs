using System;
using FaceScan.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceScan.Web.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260521102000_AddEdgeAgentHeartbeat")]
    public partial class AddEdgeAgentHeartbeat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EdgeAgentHeartbeats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StationCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AgentId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    LastIpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EdgeAgentHeartbeats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EdgeAgentHeartbeats_LastSeenAtUtc",
                table: "EdgeAgentHeartbeats",
                column: "LastSeenAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_EdgeAgentHeartbeats_StationCode_AgentId",
                table: "EdgeAgentHeartbeats",
                columns: new[] { "StationCode", "AgentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EdgeAgentHeartbeats");
        }
    }
}