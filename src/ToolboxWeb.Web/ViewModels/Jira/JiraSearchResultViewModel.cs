namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>Answer of POST /Jira/Search for the Search tab (stage 5.2). Raw values; the page formats them.</summary>
public sealed class JiraSearchResultViewModel
{
    public IReadOnlyList<JiraSearchRowViewModel> Rows { get; init; } = [];
    public string Jql { get; init; } = string.Empty;

    /// <summary>Jira returns at most this many issues per search; Truncated tells the page to say so.</summary>
    public int Limit { get; init; }
    public bool Truncated { get; init; }
}

public sealed class JiraSearchRowViewModel
{
    public string IssueKey { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string StatusTone { get; init; } = "todo";
    public string Type { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public bool IsOverdue { get; init; }
    public int DaysOverdue { get; init; }
    public string Parent { get; init; } = string.Empty;
    public string ParentSummary { get; init; } = string.Empty;
    public decimal EstimateHours { get; init; }

    /// <summary>All hours logged on the issue, by anyone.</summary>
    public decimal LoggedHours { get; init; }

    /// <summary>Hours per day (yyyy-MM-dd), shown when hovering the logged hours.</summary>
    public IReadOnlyList<JiraLogDayViewModel> LogDays { get; init; } = [];
}

public sealed record JiraLogDayViewModel(string Date, decimal Hours);
