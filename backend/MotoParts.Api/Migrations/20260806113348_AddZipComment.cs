using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddZipComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "Zips",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "Zips");
        }
    }
}
