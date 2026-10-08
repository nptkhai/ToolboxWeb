using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ToolboxWeb.Web.Migrations
{
    /// <summary>
    /// A server group no longer holds a connection itself; each connection is a sub-note with
    /// its own title. A group that was saved with connection details keeps its title and
    /// description, and those details move into a first sub-note named after the group. The
    /// password moves as ciphertext: it is protected per user, not per note, so it still decrypts.
    /// </summary>
    public partial class MoveServerDetailsToSubNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "Notes" ("UserId", "ParentId", "Title", "Content", "Tags", "IsPinned", "Format", "CreatedAt", "UpdatedAt",
                                     "Server_Host", "Server_Port", "Server_Username", "Server_Database", "Server_ProtectedPassword")
                SELECT "UserId", "Id", "Title", '', NULL, 0, 7, "CreatedAt", "UpdatedAt",
                       "Server_Host", "Server_Port", "Server_Username", "Server_Database", "Server_ProtectedPassword"
                FROM "Notes"
                WHERE "ParentId" IS NULL AND "Format" = 7
                  AND ("Server_Host" IS NOT NULL OR "Server_Port" IS NOT NULL OR "Server_Username" IS NOT NULL
                       OR "Server_Database" IS NOT NULL OR "Server_ProtectedPassword" IS NOT NULL);
                """);

            migrationBuilder.Sql("""
                UPDATE "Notes"
                SET "Server_Host" = NULL, "Server_Port" = NULL, "Server_Username" = NULL,
                    "Server_Database" = NULL, "Server_ProtectedPassword" = NULL
                WHERE "ParentId" IS NULL AND "Format" = 7;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data only, and nothing is lost going forward: the moved sub-notes stay where they are.
        }
    }
}
