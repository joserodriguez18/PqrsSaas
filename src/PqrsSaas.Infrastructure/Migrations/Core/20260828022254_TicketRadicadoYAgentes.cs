using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PqrsSaas.Infrastructure.Migrations.Core
{
    /// <inheritdoc />
    public partial class TicketRadicadoYAgentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Activo",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCreacion",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacion",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "NumeroRadicado",
                table: "Tickets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_NumeroRadicado",
                table: "Tickets",
                column: "NumeroRadicado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeBaseArticles_Embedding",
                table: "KnowledgeBaseArticles",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tickets_NumeroRadicado",
                table: "Tickets");

            migrationBuilder.DropIndex(
                name: "IX_KnowledgeBaseArticles_Embedding",
                table: "KnowledgeBaseArticles");

            migrationBuilder.DropColumn(
                name: "Activo",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FechaCreacion",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "NumeroRadicado",
                table: "Tickets");
        }
    }
}
