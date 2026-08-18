using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDeliveryCompanyAndComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeliveryComment",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryCompany",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryComment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCompany",
                table: "Orders");
        }
    }
}
