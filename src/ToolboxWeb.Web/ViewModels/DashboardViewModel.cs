using ToolboxWeb.Web.Domain;

namespace ToolboxWeb.Web.ViewModels;

public class DashboardViewModel
{
    public IReadOnlyList<Note> RecentNotes { get; init; } = [];
    public IReadOnlyList<ChecklistItem> TodayChecklist { get; init; } = [];
    public IReadOnlyList<QuickLink> QuickLinks { get; init; } = [];
    public IReadOnlyList<PromptTemplate> PromptTemplates { get; init; } = [];
    public IReadOnlyList<FocusSession> RecentFocusSessions { get; init; } = [];
    public IReadOnlyList<ActivityLog> RecentActivity { get; init; } = [];
    public NoteFormViewModel QuickNote { get; init; } = new();
    public ChecklistItemFormViewModel QuickTask { get; init; } = new();
}
