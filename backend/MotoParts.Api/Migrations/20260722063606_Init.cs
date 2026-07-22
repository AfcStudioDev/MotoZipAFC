using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IncomeMotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeMotos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotoMarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Mark = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotoMarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNum = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNumbers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordResetTokenHash = table.Column<string>(type: "text", nullable: true),
                    PasswordResetTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    IsSender = table.Column<bool>(type: "boolean", nullable: false),
                    IsRegistrar = table.Column<bool>(type: "boolean", nullable: false),
                    FIO = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    OAuthProvider = table.Column<string>(type: "text", nullable: true),
                    OAuthSubject = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZipGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GroupName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZipGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MotoModels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MarkId = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotoModels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MotoModels_MotoMarks_MarkId",
                        column: x => x.MarkId,
                        principalTable: "MotoMarks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAdressess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "text", nullable: false),
                    PostCode = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAdressess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryAdressess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Zips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    IncomeCost = table.Column<decimal>(type: "numeric", nullable: false),
                    PartNumberId = table.Column<int>(type: "integer", nullable: true),
                    MarkId = table.Column<int>(type: "integer", nullable: true),
                    ModelId = table.Column<int>(type: "integer", nullable: true),
                    GroupId = table.Column<int>(type: "integer", nullable: true),
                    Year = table.Column<DateOnly>(type: "date", nullable: true),
                    IncomeMotoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Zips_IncomeMotos_IncomeMotoId",
                        column: x => x.IncomeMotoId,
                        principalTable: "IncomeMotos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zips_MotoMarks_MarkId",
                        column: x => x.MarkId,
                        principalTable: "MotoMarks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zips_MotoModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "MotoModels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Zips_PartNumbers_PartNumberId",
                        column: x => x.PartNumberId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Zips_ZipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ZipGroups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Movements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CountOrdered = table.Column<int>(type: "integer", nullable: false),
                    NomenclatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OrderDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OperationTypeId = table.Column<short>(type: "smallint", nullable: true),
                    SellCost = table.Column<decimal>(type: "numeric", nullable: false),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Movements_DeliveryAdressess_AddressId",
                        column: x => x.AddressId,
                        principalTable: "DeliveryAdressess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Operations_OperationTypeId",
                        column: x => x.OperationTypeId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Movements_Zips_NomenclatureId",
                        column: x => x.NomenclatureId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNumber = table.Column<string>(type: "text", nullable: false),
                    CountOrdered = table.Column<int>(type: "integer", nullable: false),
                    NomenclatureId = table.Column<Guid>(type: "uuid", nullable: true),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OrderDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_DeliveryAdressess_AddressId",
                        column: x => x.AddressId,
                        principalTable: "DeliveryAdressess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Orders_Zips_NomenclatureId",
                        column: x => x.NomenclatureId,
                        principalTable: "Zips",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Stored",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZipId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stored", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stored_Zips_ZipId",
                        column: x => x.ZipId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    YooKassaPaymentId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAdressess_UserId",
                table: "DeliveryAdressess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MotoMarks_Mark",
                table: "MotoMarks",
                column: "Mark",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MotoModels_MarkId",
                table: "MotoModels",
                column: "MarkId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_AddressId",
                table: "Movements",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_NomenclatureId",
                table: "Movements",
                column: "NomenclatureId");

            migrationBuilder.CreateIndex(
                name: "IX_Movements_OperationTypeId",
                table: "Movements",
                column: "OperationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AddressId",
                table: "Orders",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_NomenclatureId",
                table: "Orders",
                column: "NomenclatureId");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumbers_PartNum",
                table: "PartNumbers",
                column: "PartNum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stored_ZipId",
                table: "Stored",
                column: "ZipId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Zips_GroupId",
                table: "Zips",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_IncomeMotoId",
                table: "Zips",
                column: "IncomeMotoId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_MarkId",
                table: "Zips",
                column: "MarkId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_ModelId",
                table: "Zips",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_PartNumberId",
                table: "Zips",
                column: "PartNumberId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Movements");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Stored");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "DeliveryAdressess");

            migrationBuilder.DropTable(
                name: "Zips");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "IncomeMotos");

            migrationBuilder.DropTable(
                name: "MotoModels");

            migrationBuilder.DropTable(
                name: "PartNumbers");

            migrationBuilder.DropTable(
                name: "ZipGroups");

            migrationBuilder.DropTable(
                name: "MotoMarks");
        }
    }
}
