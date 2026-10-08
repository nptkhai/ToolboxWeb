using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteFormatsAndSubNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Format",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "Notes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Server_Database",
                table: "Notes",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Server_Host",
                table: "Notes",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Server_Port",
                table: "Notes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Server_ProtectedPassword",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Server_Username",
                table: "Notes",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_ParentId",
                table: "Notes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserId_ParentId",
                table: "Notes",
                columns: new[] { "UserId", "ParentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Notes_ParentId",
                table: "Notes",
                column: "ParentId",
                principalTable: "Notes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Notes_ParentId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_ParentId",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_UserId_ParentId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Server_Database",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Server_Host",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Server_Port",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Server_ProtectedPassword",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "Server_Username",
                table: "Notes");
        }
    }
}
