using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFocusSessionTodoLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChecklistItemId",
                table: "FocusSessions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FocusSessions_ChecklistItemId",
                table: "FocusSessions",
                column: "ChecklistItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_FocusSessions_ChecklistItems_ChecklistItemId",
                table: "FocusSessions",
                column: "ChecklistItemId",
                principalTable: "ChecklistItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FocusSessions_ChecklistItems_ChecklistItemId",
                table: "FocusSessions");

            migrationBuilder.DropIndex(
                name: "IX_FocusSessions_ChecklistItemId",
                table: "FocusSessions");

            migrationBuilder.DropColumn(
                name: "ChecklistItemId",
                table: "FocusSessions");
        }
    }
}
