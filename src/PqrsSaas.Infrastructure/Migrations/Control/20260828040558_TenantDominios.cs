using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PqrsSaas.Infrastructure.Migrations.Control
{
    /// <inheritdoc />
    public partial class TenantDominios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantDominios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Origen = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantDominios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantDominios_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantDominios_TenantId_Origen",
                table: "TenantDominios",
                columns: new[] { "TenantId", "Origen" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantDominios");
        }
    }
}
