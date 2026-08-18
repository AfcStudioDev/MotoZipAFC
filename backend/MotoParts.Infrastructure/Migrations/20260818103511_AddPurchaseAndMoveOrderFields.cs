using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchaseAndMoveOrderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Сгенерированная EF миграция сначала роняла AddressId/UserId/IsPaid/... с Orders
            // и только потом создавала Purchases — данные терялись безвозвратно, а PurchaseId
            // проставлялся заглушкой (нулевой Guid), которая не существует как Purchase, и
            // AddForeignKey упал бы на первой же существующей строке. Переписано вручную:
            // сначала создаём Purchases и переносим туда данные, потом чистим Orders.

            migrationBuilder.CreateTable(
                name: "Purchases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseNumber = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OrderDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryCompany = table.Column<string>(type: "text", nullable: true),
                    DeliveryComment = table.Column<string>(type: "text", nullable: true),
                    ReceiptFileName = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Purchases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Purchases_DeliveryAddressess_AddressId",
                        column: x => x.AddressId,
                        principalTable: "DeliveryAddressess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Purchases_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Один Order = один Purchase на старых данных: переиспользуем Id заказа как Id
            // покупки — тогда backfill в Orders не требует join, только "PurchaseId = Id".
            migrationBuilder.Sql(@"
                INSERT INTO ""Purchases""
                    (""Id"", ""PurchaseNumber"", ""UserId"", ""AddressId"", ""OrderDateTime"",
                     ""IsPaid"", ""DeliveryCompany"", ""DeliveryComment"", ""ReceiptFileName"")
                SELECT
                    ""Id"",
                    'PUR-' || (EXTRACT(EPOCH FROM ""OrderDateTime""))::bigint::text || '-' || substr(""Id""::text, 1, 8),
                    ""UserId"", ""AddressId"", ""OrderDateTime"",
                    ""IsPaid"", ""DeliveryCompany"", ""DeliveryComment"", ""ReceiptFileName""
                FROM ""Orders"";
            ");

            migrationBuilder.AddColumn<Guid>(
                name: "PurchaseId",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"UPDATE ""Orders"" SET ""PurchaseId"" = ""Id"";");

            migrationBuilder.AlterColumn<Guid>(
                name: "PurchaseId",
                table: "Orders",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_DeliveryAddressess_AddressId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_AddressId",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_UserId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AddressId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryComment",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveryCompany",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ReceiptFileName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PurchaseId",
                table: "Orders",
                column: "PurchaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_AddressId",
                table: "Purchases",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Purchases_UserId",
                table: "Purchases",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Purchases_PurchaseId",
                table: "Orders",
                column: "PurchaseId",
                principalTable: "Purchases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Purchases_PurchaseId",
                table: "Orders");

            migrationBuilder.AddColumn<int>(
                name: "AddressId",
                table: "Orders",
                type: "integer",
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                table: "Orders",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptFileName",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Orders",
                type: "integer",
                nullable: true);

            // Возвращаем значения из Purchase обратно на Order — для многотоварных покупок,
            // созданных уже после этой миграции, это потеряет группировку (каждая позиция
            // станет независимым заказом), но Down() здесь и не обязан быть лосслесс -
            // это откат схемы, а не бизнес-функциональности.
            migrationBuilder.Sql(@"
                UPDATE ""Orders"" o
                SET ""AddressId"" = p.""AddressId"",
                    ""UserId"" = p.""UserId"",
                    ""IsPaid"" = p.""IsPaid"",
                    ""DeliveryCompany"" = p.""DeliveryCompany"",
                    ""DeliveryComment"" = p.""DeliveryComment"",
                    ""ReceiptFileName"" = p.""ReceiptFileName""
                FROM ""Purchases"" p
                WHERE o.""PurchaseId"" = p.""Id"";
            ");

            migrationBuilder.AlterColumn<int>(
                name: "AddressId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "Orders",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.DropTable(
                name: "Purchases");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PurchaseId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PurchaseId",
                table: "Orders");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AddressId",
                table: "Orders",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_DeliveryAddressess_AddressId",
                table: "Orders",
                column: "AddressId",
                principalTable: "DeliveryAddressess",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
