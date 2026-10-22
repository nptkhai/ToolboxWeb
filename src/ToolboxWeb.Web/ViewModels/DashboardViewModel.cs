using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class DashboardViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>Now in the app time zone, for the greeting.</summary>
    public DateTime Now { get; init; }
    /// <summary>The date the tasks were classified against, so the view labels them consistently.</summary>
    public DateTime Today { get; init; }

    public int OverdueCount { get; init; }
    public int DueTodayCount { get; init; }
    public int OpenTaskCount { get; init; }
    /// <summary>Open (not done) tasks by urgency; each group is capped, so this may hold fewer than OpenTaskCount.</summary>
    public IReadOnlyList<DashboardTaskGroupViewModel> TaskGroups { get; init; } = [];

    public int FocusMinutesToday { get; init; }
    public int FocusMinutesWeek { get; init; }
    public IReadOnlyList<FocusSession> RecentFocusSessions { get; init; } = [];

    /// <summary>A Jira session is open in this browser, so the Jira hours card can load.</summary>
    public bool HasJiraSession { get; init; }

    public IReadOnlyList<Note> RecentNotes { get; init; } = [];
    public IReadOnlyList<QuickLink> QuickLinks { get; init; } = [];
    public IReadOnlyList<PromptTemplate> PromptTemplates { get; init; } = [];
    public IReadOnlyList<ActivityLog> RecentActivity { get; init; } = [];

    public NoteFormViewModel QuickNote { get; set; } = new();
    public bool OpenQuickNote { get; set; }
    public DashboardQuickTaskViewModel QuickTask { get; init; } = new();
}

public enum DashboardTaskGroup
{
    Overdue,
    Today,
    Upcoming,
    NoDueDate
}

public class DashboardTaskGroupViewModel
{
    public DashboardTaskGroup Group { get; init; }
    public IReadOnlyList<ChecklistItem> Items { get; init; } = [];
    public int Total { get; init; }
}
