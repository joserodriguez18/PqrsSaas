using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PqrsSaas.Infrastructure.Migrations.Control
{
    /// <inheritdoc />
    public partial class InitialControlDb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    DominioPermitido = table.Column<string>(type: "text", nullable: false),
                    ApiKeyWidget = table.Column<string>(type: "text", nullable: false),
                    NombreBaseDatos = table.Column<string>(type: "text", nullable: false),
                    EstadoProvisionamiento = table.Column<string>(type: "text", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaCreacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantConfiguraciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ColorPrimario = table.Column<string>(type: "text", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    UmbralSimilitudRAG = table.Column<double>(type: "double precision", nullable: false),
                    LimiteTicketsMes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantConfiguraciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantConfiguraciones_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantConfiguraciones_TenantId",
                table: "TenantConfiguraciones",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_ApiKeyWidget",
                table: "Tenants",
                column: "ApiKeyWidget",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantConfiguraciones");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
