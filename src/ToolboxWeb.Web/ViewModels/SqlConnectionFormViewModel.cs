namespace ToolboxWeb.Web.ViewModels;

/// <summary>
/// Input for Views/Shared/_SqlConnectionForm.cshtml, the SQL Server connection form shared by
/// Schema compare and SQL capture. Behavior: Content/js/sql-connection.js.
/// </summary>
public sealed class SqlConnectionFormViewModel
{
    /// <summary>Prefix for the input ids, unique on the page.</summary>
    public required string IdPrefix { get; init; }

    /// <summary>The page's TestConnection endpoint.</summary>
    public required string TestUrl { get; init; }

    /// <summary>Optional role of this connection on the page (for example source or target).</summary>
    public string? Side { get; init; }

    /// <summary>Optional heading inside the form; leave empty when the page section already has one.</summary>
    public string? Title { get; init; }

    /// <summary>Optional short tag next to the heading.</summary>
    public string? Tag { get; init; }

    /// <summary>Without a card frame, for a form that already sits in a panel.</summary>
    public bool Plain { get; init; }

    public bool ShowHostAllowlistNotice { get; init; }
}
