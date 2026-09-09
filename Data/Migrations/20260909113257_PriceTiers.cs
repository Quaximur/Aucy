using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Aucy.Data.Migrations
{
    /// <inheritdoc />
    public partial class PriceTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PriceTierCount",
                table: "ItemConfigs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceTierCount",
                table: "ItemConfigs");
        }
    }
}
