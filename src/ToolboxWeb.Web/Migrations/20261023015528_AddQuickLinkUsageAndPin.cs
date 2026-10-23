using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddQuickLinkUsageAndPin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "QuickLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastOpenedAt",
                table: "QuickLinks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OpenCount",
                table: "QuickLinks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "QuickLinks");

            migrationBuilder.DropColumn(
                name: "LastOpenedAt",
                table: "QuickLinks");

            migrationBuilder.DropColumn(
                name: "OpenCount",
                table: "QuickLinks");
        }
    }
}
