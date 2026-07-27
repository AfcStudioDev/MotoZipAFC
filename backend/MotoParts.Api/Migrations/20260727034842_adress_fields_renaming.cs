using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class adress_fields_renaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Address",
                table: "DeliveryAdresses",
                newName: "Adress");

            migrationBuilder.AddColumn<int>(
                name: "AdressId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdressId",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "Adress",
                table: "DeliveryAdresses",
                newName: "Address");
        }
    }
}
