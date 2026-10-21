using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class DashboardViewModel
{
    public IReadOnlyList<Note> RecentNotes { get; init; } = [];
    /// <summary>Open (not done) tasks, most urgent first: overdue, due today, upcoming, no due date.</summary>
    public IReadOnlyList<ChecklistItem> OpenTasks { get; init; } = [];
    /// <summary>The date OpenTasks were classified against, so the view labels them consistently.</summary>
    public DateTime Today { get; init; }
    public IReadOnlyList<QuickLink> QuickLinks { get; init; } = [];
    public IReadOnlyList<PromptTemplate> PromptTemplates { get; init; } = [];
    public IReadOnlyList<FocusSession> RecentFocusSessions { get; init; } = [];
    public IReadOnlyList<ActivityLog> RecentActivity { get; init; } = [];
    public NoteFormViewModel QuickNote { get; init; } = new();
    public ChecklistItemFormViewModel QuickTask { get; init; } = new();
}
