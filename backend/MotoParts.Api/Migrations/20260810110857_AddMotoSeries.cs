using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMotoSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MotoSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MotoSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PartNumberSeriesApplicabilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNumId = table.Column<int>(type: "integer", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNumberSeriesApplicabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PartNumberSeriesApplicabilities_MotoSeries_SeriesId",
                        column: x => x.SeriesId,
                        principalTable: "MotoSeries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PartNumberSeriesApplicabilities_PartNumbers_PartNumId",
                        column: x => x.PartNumId,
                        principalTable: "PartNumbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PartNumberSeriesApplicabilities_PartNumId_SeriesId",
                table: "PartNumberSeriesApplicabilities",
                columns: new[] { "PartNumId", "SeriesId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PartNumberSeriesApplicabilities_SeriesId",
                table: "PartNumberSeriesApplicabilities",
                column: "SeriesId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PartNumberSeriesApplicabilities");

            migrationBuilder.DropTable(
                name: "MotoSeries");
        }
    }
}
