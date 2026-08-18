using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderReceiptFileName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReceiptFileName",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReceiptFileName",
                table: "Orders");
        }
    }
}
