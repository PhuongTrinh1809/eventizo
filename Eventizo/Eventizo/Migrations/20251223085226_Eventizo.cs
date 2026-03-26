using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Eventizo.Migrations
{
    /// <inheritdoc />
    public partial class Eventizo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CheckedInAt",
                table: "Tickets",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCheckedIn",
                table: "Tickets",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedInAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "IsCheckedIn",
                table: "Tickets");
        }
    }
}
