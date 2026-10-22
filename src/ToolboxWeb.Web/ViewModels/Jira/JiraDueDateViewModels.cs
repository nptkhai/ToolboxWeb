namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>Body of POST /Jira/UpdateDueDates: due dates moved on the board, to write to Jira.</summary>
public sealed class JiraDueDateUpdateRequest
{
    public List<JiraDueDateChange> Changes { get; set; } = [];
}

/// <summary>One move. Dates are yyyy-MM-dd; null or empty means "no due date".</summary>
public sealed class JiraDueDateChange
{
    public string IssueKey { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public string? PreviousDueDate { get; set; }
}

public sealed record JiraDueDateResult(string IssueKey, bool Success, string? Message);
