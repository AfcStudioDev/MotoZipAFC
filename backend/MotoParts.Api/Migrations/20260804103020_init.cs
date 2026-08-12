using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeliveryStatuses",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryStatuses", x => x.Id);
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
                name: "OperationTypes",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
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
                name: "Operations",
                columns: table => new
                {
                    Id = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeId = table.Column<short>(type: "smallint", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Operations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Operations_OperationTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "OperationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryAddressess",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<string>(type: "text", nullable: false),
                    PostCode = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryAddressess", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryAddressess_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "IncomeMotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeMotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeMotos_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartNumbers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNum = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNumbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartNumbers_ZipGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ZipGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PartNumberApplicabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNumId = table.Column<int>(type: "integer", nullable: false),
                    ModelId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNumberApplicabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartNumberApplicabilities_MotoModels_ModelId",
                        column: x => x.ModelId,
                        principalTable: "MotoModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartNumberApplicabilities_PartNumbers_PartNumId",
                        column: x => x.PartNumId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Zips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeCost = table.Column<decimal>(type: "numeric", nullable: false),
                    SellCost = table.Column<decimal>(type: "numeric", nullable: true),
                    PartNumId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<short>(type: "smallint", nullable: true),
                    IncomeMotoId = table.Column<Guid>(type: "uuid", nullable: false),
                    IncomeDate = table.Column<DateOnly>(type: "date", nullable: true)
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
                        name: "FK_Zips_PartNumbers_PartNumId",
                        column: x => x.PartNumId,
                        principalTable: "PartNumbers",
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
                    ZipId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressId = table.Column<int>(type: "integer", nullable: false),
                    OrderDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SellCost = table.Column<decimal>(type: "numeric", nullable: false),
                    OperationId = table.Column<short>(type: "smallint", nullable: true),
                    Discount = table.Column<decimal>(type: "numeric", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DeliveryStatusId = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_DeliveryAddressess_AddressId",
                        column: x => x.AddressId,
                        principalTable: "DeliveryAddressess",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_DeliveryStatuses_DeliveryStatusId",
                        column: x => x.DeliveryStatusId,
                        principalTable: "DeliveryStatuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Zips_ZipId",
                        column: x => x.ZipId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZipId = table.Column<Guid>(type: "uuid", nullable: false),
                    OldCost = table.Column<decimal>(type: "numeric", nullable: false),
                    NewCost = table.Column<decimal>(type: "numeric", nullable: false),
                    OperationId = table.Column<short>(type: "smallint", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceHistories_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PriceHistories_Zips_ZipId",
                        column: x => x.ZipId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "ZipPhotos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ZipId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZipPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZipPhotos_Zips_ZipId",
                        column: x => x.ZipId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Logs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    OperationId = table.Column<short>(type: "smallint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    ZipId = table.Column<Guid>(type: "uuid", nullable: true),
                    Qty = table.Column<int>(type: "integer", nullable: true),
                    UnitCost = table.Column<decimal>(type: "numeric", nullable: true),
                    SellCost = table.Column<decimal>(type: "numeric", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Logs_Operations_OperationId",
                        column: x => x.OperationId,
                        principalTable: "Operations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Logs_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Logs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Logs_Zips_ZipId",
                        column: x => x.ZipId,
                        principalTable: "Zips",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryAddressess_UserId",
                table: "DeliveryAddressess",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeMotos_UserId",
                table: "IncomeMotos",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_OperationId_CreatedAt",
                table: "Logs",
                columns: new[] { "OperationId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_OrderId",
                table: "Logs",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_UserId",
                table: "Logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_ZipId_CreatedAt",
                table: "Logs",
                columns: new[] { "ZipId", "CreatedAt" });

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
                name: "IX_Operations_TypeId",
                table: "Operations",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AddressId",
                table: "Orders",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DeliveryStatusId",
                table: "Orders",
                column: "DeliveryStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OperationId",
                table: "Orders",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ZipId",
                table: "Orders",
                column: "ZipId");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumberApplicabilities_ModelId",
                table: "PartNumberApplicabilities",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumberApplicabilities_PartNumId_ModelId",
                table: "PartNumberApplicabilities",
                columns: new[] { "PartNumId", "ModelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartNumbers_GroupId",
                table: "PartNumbers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumbers_PartNum",
                table: "PartNumbers",
                column: "PartNum",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_OperationId",
                table: "PriceHistories",
                column: "OperationId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_UserId",
                table: "PriceHistories",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceHistories_ZipId_CreatedAt",
                table: "PriceHistories",
                columns: new[] { "ZipId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Stored_ZipId",
                table: "Stored",
                column: "ZipId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ZipPhotos_ZipId",
                table: "ZipPhotos",
                column: "ZipId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_IncomeMotoId",
                table: "Zips",
                column: "IncomeMotoId");

            migrationBuilder.CreateIndex(
                name: "IX_Zips_PartNumId",
                table: "Zips",
                column: "PartNumId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logs");

            migrationBuilder.DropTable(
                name: "PartNumberApplicabilities");

            migrationBuilder.DropTable(
                name: "PriceHistories");

            migrationBuilder.DropTable(
                name: "Stored");

            migrationBuilder.DropTable(
                name: "ZipPhotos");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "MotoModels");

            migrationBuilder.DropTable(
                name: "DeliveryAddressess");

            migrationBuilder.DropTable(
                name: "DeliveryStatuses");

            migrationBuilder.DropTable(
                name: "Operations");

            migrationBuilder.DropTable(
                name: "Zips");

            migrationBuilder.DropTable(
                name: "MotoMarks");

            migrationBuilder.DropTable(
                name: "OperationTypes");

            migrationBuilder.DropTable(
                name: "IncomeMotos");

            migrationBuilder.DropTable(
                name: "PartNumbers");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "ZipGroups");
        }
    }
}
