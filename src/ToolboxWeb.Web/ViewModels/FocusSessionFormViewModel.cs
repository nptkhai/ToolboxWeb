using System.ComponentModel.DataAnnotations;

namespace ToolboxWeb.Web.ViewModels;

public class FocusSessionFormViewModel
{
    [Range(1, 240, ErrorMessage = "Validation.Range")]
    public int DurationMinutes { get; set; } = 25;

    [StringLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? Note { get; set; }

    /// <summary>The to-do worked on; ignored unless it is one of the user's.</summary>
    public int? ChecklistItemId { get; set; }

    /// <summary>
    /// When the timer started, in ms since 1970 (browser clock). The same session can finish in
    /// two open tabs; the start time lets the server keep only one of them.
    /// </summary>
    public long? StartedAtMs { get; set; }

    /// <summary>
    /// When the session really ended, in ms since 1970 (browser clock). A session that ran out
    /// while the browser was closed is sent later; this keeps it on the day it ended.
    /// </summary>
    public long? EndedAtMs { get; set; }
}

public class FocusSessionNoteViewModel
{
    public int Id { get; set; }

    [StringLength(500, ErrorMessage = "Validation.MaxLength")]
    public string? Note { get; set; }
}

public class FocusTimerPageViewModel
{
    public FocusSessionFormViewModel Form { get; init; } = new();
    public ToolboxWeb.Web.Services.FocusPageStats Stats { get; init; } = new(0, 0, [], []);

    /// <summary>Open to-dos the session can be linked to.</summary>
    public IReadOnlyList<FocusTodoOption> Todos { get; init; } = [];

    /// <summary>?start=true from "Start 25 minutes" on the overview.</summary>
    public bool AutoStart { get; init; }

    /// <summary>?minutes=… from the overview; null keeps the remembered length.</summary>
    public int? RequestedMinutes { get; init; }
}

public sealed record FocusTodoOption(int Id, string Title);
