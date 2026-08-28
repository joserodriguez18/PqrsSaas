using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PqrsSaas.Infrastructure.Migrations.Core
{
    /// <inheritdoc />
    public partial class TituloArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "KnowledgeBaseArticles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "KnowledgeBaseArticles");
        }
    }
}
