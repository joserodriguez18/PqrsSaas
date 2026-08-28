using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PqrsSaas.Infrastructure.Migrations.Core
{
    /// <inheritdoc />
    public partial class DebeCambiarPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DebeCambiarPassword",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DebeCambiarPassword",
                table: "Users");
        }
    }
}
