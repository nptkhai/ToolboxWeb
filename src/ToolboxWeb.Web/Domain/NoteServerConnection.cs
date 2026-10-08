namespace ToolboxWeb.Web.Domain;

/// <summary>
/// Connection details for a note in the Server format. Owned by <see cref="Note"/> and stored
/// as columns on the Notes table.
/// </summary>
public class NoteServerConnection
{
    public string? Host { get; set; }
    public int? Port { get; set; }
    public string? Username { get; set; }
    public string? Database { get; set; }

    /// <summary>
    /// Ciphertext produced by <c>INoteSecretProtector</c>, never the password itself, so a copy
    /// of the database file alone does not give the password away.
    /// </summary>
    public string? ProtectedPassword { get; set; }
}
