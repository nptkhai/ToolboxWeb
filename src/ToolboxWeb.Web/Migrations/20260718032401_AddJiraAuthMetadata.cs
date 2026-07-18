using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJiraAuthMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthSource",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraBaseUrl",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraDisplayName",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JiraUsername",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthSource",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "JiraBaseUrl",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "JiraDisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "JiraUsername",
                table: "AspNetUsers");
        }
    }
}
