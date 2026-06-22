using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptTemplateStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InputVariables",
                table: "PromptTemplates",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OutputFormat",
                table: "PromptTemplates",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InputVariables",
                table: "PromptTemplates");

            migrationBuilder.DropColumn(
                name: "OutputFormat",
                table: "PromptTemplates");
        }
    }
}
