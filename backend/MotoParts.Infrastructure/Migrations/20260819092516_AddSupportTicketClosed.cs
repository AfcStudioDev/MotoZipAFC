using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MotoParts.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTicketClosed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "SupportTickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "SupportTickets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "SupportTickets");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "SupportTickets");
        }
    }
}
