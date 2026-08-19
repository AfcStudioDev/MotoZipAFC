using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderPriceCostAndDiscountPercent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                table: "Orders",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PriceCost",
                table: "Orders",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PriceCost",
                table: "Orders");
        }
    }
}
