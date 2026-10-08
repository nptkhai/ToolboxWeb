namespace ToolboxWeb.Web.ViewModels;

/// <summary>One row of the note list.</summary>
public sealed class NoteListItemViewModel
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public required NoteFormatInfo Format { get; init; }
    public bool IsPinned { get; init; }
    public int ChildCount { get; init; }

    /// <summary>First meaningful line of the content, or the host for a server note.</summary>
    public string Preview { get; init; } = string.Empty;

    public DateTime UpdatedUtc { get; init; }

    /// <summary>The search hit is inside a sub-note rather than the group itself.</summary>
    public bool MatchedInChild { get; init; }

    public bool IsSelected { get; init; }
}

public sealed class NoteFormatCount
{
    public required NoteFormatInfo Format { get; init; }
    public int Count { get; init; }
}

/// <summary>The connection fields of a server note. The password itself is never here.</summary>
public sealed class NoteServerViewModel
{
    public string? Host { get; init; }
    public int? Port { get; init; }
    public string? Username { get; init; }
    public string? Database { get; init; }
    public bool HasPassword { get; init; }

    public string Display => string.IsNullOrWhiteSpace(Host)
        ? string.Empty
        : Port is null ? Host : $"{Host}:{Port}";

    /// <summary>One line for a collapsed connection: <c>user@host:port · database</c>.</summary>
    public string Summary
    {
        get
        {
            var target = Display.Length == 0 || string.IsNullOrWhiteSpace(Username) ? Display : $"{Username}@{Display}";
            if (string.IsNullOrWhiteSpace(Database))
            {
                return target;
            }

            return target.Length == 0 ? Database : $"{target} · {Database}";
        }
    }
}

/// <summary>
/// Input to the server-fields partial, which appears in several forms on one page. The id
/// prefix keeps every label/input pair unique so no two instances collide.
/// </summary>
/// <param name="NoteId">Set for a saved note; enables reveal/copy of the stored password.</param>
/// <param name="RequireHost">
/// False where the fields are only conditionally visible (the create dialog, a Mixed group's
/// add form): a hidden <c>required</c> input would block submission. The server validates instead.
/// </param>
public sealed record NoteServerFieldsModel(string IdPrefix, NoteServerViewModel Server, int? NoteId, bool RequireHost);

/// <summary>One row of a link note: an optional name and the address itself.</summary>
public sealed record NoteLinkRow(string Name, string Url)
{
    /// <summary>Only http(s) may be opened from the page; anything else is text in a box.</summary>
    public bool IsWeb =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

/// <summary>One row of a checklist note.</summary>
public sealed record NoteTaskRow(string Text, NoteTaskStatusInfo Status);

/// <summary>Input to the link-rows and checklist-rows partials.</summary>
/// <param name="Conditional">
/// True inside a Mixed group, where the block is shown only while its format is the one picked.
/// </param>
public sealed record NoteLinkRowsModel(IReadOnlyList<NoteLinkRow> Rows, bool Conditional);

public sealed record NoteTaskRowsModel(IReadOnlyList<NoteTaskRow> Rows, bool Conditional);

/// <summary>Input to the content-field partial: the textarea plus its copy/download/format tools.</summary>
/// <param name="Id">Unique id of the textarea; tools find it through this.</param>
/// <param name="FileName">Title used for the downloaded file name.</param>
public sealed record NoteContentFieldModel(
    string Id,
    string Content,
    NoteFormatInfo Format,
    string FileName,
    string LabelKey,
    string? Placeholder,
    IReadOnlyList<string> Links,
    int Rows);

/// <summary>Fields shared by a group and its sub-notes in the detail pane.</summary>
public abstract class NoteBodyViewModel
{
    public required int Id { get; init; }
    public required string Title { get; init; }
    public string Content { get; init; } = string.Empty;
    public required NoteFormatInfo Format { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public NoteServerViewModel Server { get; init; } = new();

    /// <summary>http(s) addresses found in a link note. Nothing else is ever rendered as a link.</summary>
    public IReadOnlyList<string> Links { get; init; } = [];

    /// <summary>Rows of a link sub-note, parsed from its text.</summary>
    public IReadOnlyList<NoteLinkRow> LinkRows { get; init; } = [];

    /// <summary>Rows of a checklist sub-note, parsed from its text.</summary>
    public IReadOnlyList<NoteTaskRow> TaskRows { get; init; } = [];
}

public sealed class NoteChildViewModel : NoteBodyViewModel
{
}

public sealed class NoteDetailViewModel : NoteBodyViewModel
{
    public string? Tags { get; init; }
    public bool IsPinned { get; init; }
    public IReadOnlyList<NoteChildViewModel> Children { get; init; } = [];
}
