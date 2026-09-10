using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels.SchemaCompare;

/// <summary>
/// Connection details typed by the user. These never leave the request that carries them:
/// the schema reader uses them to open a connection and they are discarded afterwards.
/// </summary>
public sealed class SqlConnectionInputModel
{
    [Required]
    [StringLength(200)]
    public string Server { get; set; } = string.Empty;

    /// <summary>Optional while probing the server, required when reading a schema.</summary>
    [StringLength(128)]
    public string Database { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string Password { get; set; } = string.Empty;

    public bool TrustServerCertificate { get; set; } = true;

    public bool Encrypt { get; set; } = true;

    public string Label => string.IsNullOrWhiteSpace(Database)
        ? Server.Trim()
        : $"{Server.Trim()} / {Database.Trim()}";
}
