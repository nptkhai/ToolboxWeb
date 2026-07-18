using Microsoft.EntityFrameworkCore.Migrations;

namespace ToolboxWeb.Web.Migrations;

public partial class AddUserAvatarUrl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AvatarUrl",
            table: "AspNetUsers",
            type: "TEXT",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AvatarUrl",
            table: "AspNetUsers");
    }
}
