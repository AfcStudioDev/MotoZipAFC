using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class seed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryAdressess_Users_UserId",
                table: "DeliveryAdressess");

            migrationBuilder.DropForeignKey(
                name: "FK_Movements_DeliveryAdressess_AddressId",
                table: "Movements");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryAdressess_AddressId",
                table: "Orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeliveryAdressess",
                table: "DeliveryAdressess");

            migrationBuilder.RenameTable(
                name: "DeliveryAdressess",
                newName: "DeliveryAdresses");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryAdressess_UserId",
                table: "DeliveryAdresses",
                newName: "IX_DeliveryAdresses_UserId");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryStatus",
                table: "Orders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SellCost",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeliveryAdresses",
                table: "DeliveryAdresses",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryAdresses_Users_UserId",
                table: "DeliveryAdresses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_DeliveryAdresses_AddressId",
                table: "Movements",
                column: "AddressId",
                principalTable: "DeliveryAdresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryAdresses_AddressId",
                table: "Orders",
                column: "AddressId",
                principalTable: "DeliveryAdresses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryAdresses_Users_UserId",
                table: "DeliveryAdresses");

            migrationBuilder.DropForeignKey(
                name: "FK_Movements_DeliveryAdresses_AddressId",
                table: "Movements");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryAdresses_AddressId",
                table: "Orders");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DeliveryAdresses",
                table: "DeliveryAdresses");

            migrationBuilder.DropColumn(
                name: "DeliveryStatus",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SellCost",
                table: "Orders");

            migrationBuilder.RenameTable(
                name: "DeliveryAdresses",
                newName: "DeliveryAdressess");

            migrationBuilder.RenameIndex(
                name: "IX_DeliveryAdresses_UserId",
                table: "DeliveryAdressess",
                newName: "IX_DeliveryAdressess_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DeliveryAdressess",
                table: "DeliveryAdressess",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryAdressess_Users_UserId",
                table: "DeliveryAdressess",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Movements_DeliveryAdressess_AddressId",
                table: "Movements",
                column: "AddressId",
                principalTable: "DeliveryAdressess",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryAdressess_AddressId",
                table: "Orders",
                column: "AddressId",
                principalTable: "DeliveryAdressess",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
