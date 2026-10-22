using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddChecklistPriorityAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ChecklistItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "ChecklistItems",
                type: "INTEGER",
                nullable: false,
                // Existing to-dos become Normal (ChecklistPriority.Normal = 2), not an undefined 0.
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "ChecklistItems");
        }
    }
}
