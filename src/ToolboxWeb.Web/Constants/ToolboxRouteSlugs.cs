namespace ToolboxWeb.Web.Constants;

public static class ToolboxRouteSlugs
{
    public static class Pages
    {
        public const string Dashboard = "dashboard";
        public const string Activity = "activity";
        public const string Notes = "notes";
        public const string Checklist = "checklist";
        public const string FocusTimer = "focus-timer";
        public const string QuickLinks = "quick-links";
        public const string PromptTemplates = "prompt-templates";
        public const string Components = "component";
        public const string KendoDemo = "kendo-ui-demo";
        public const string TabulatorDemo = "tabulator-demo";
        // Jira "Statistics" and "Task management" (renamed 2026-11-19; the old jira-* addresses redirect, see JiraController).
        public const string JiraDashboard = "statistics";
        public const string JiraWorklist = "tasks";
        public const string TextCompare = "text-compare";
        public const string SchemaCompare = "schema-compare";
        public const string SqlProfiler = "sql-profiler";
        public const string Privacy = "privacy";
        public const string Settings = "settings";
        public const string Activate = "activate";
        public const string Update = "update";
        public const string Error = "error";
    }

    public static class HtmlPages
    {
        public const string Dashboard = "dashboard.html";
        public const string Activity = "activity.html";
        public const string Notes = "notes.html";
        public const string Checklist = "checklist.html";
        public const string FocusTimer = "focus-timer.html";
        public const string QuickLinks = "quick-links.html";
        public const string PromptTemplates = "prompt-templates.html";
        public const string Components = "component.html";
        public const string KendoDemo = "demo-kendo.html";
        public const string TabulatorDemo = "tabulator-demo.html";
        public const string JiraDashboard = "statistics.html";
        public const string JiraWorklist = "tasks.html";
        public const string TextCompare = "text-compare.html";
        public const string SchemaCompare = "schema-compare.html";
        public const string SqlProfiler = "sql-profiler.html";
        public const string Privacy = "privacy.html";
        public const string Settings = "settings.html";
        public const string Activate = "activate.html";
        public const string Update = "update.html";
        public const string Error = "error.html";
    }

    /// <summary>Addresses used before the Jira screens were renamed; they redirect to the new ones.</summary>
    public static class LegacyPages
    {
        public const string JiraDashboard = "jira-dashboard";
        public const string JiraDashboardHtml = "jira-dashboard.html";
        public const string JiraWorklist = "jira-worklist";
        public const string JiraWorklistHtml = "jira-worklist.html";
    }

    private static readonly string[] JiraScreenPaths =
    [
        Pages.JiraDashboard, HtmlPages.JiraDashboard, Pages.JiraWorklist, HtmlPages.JiraWorklist,
        LegacyPages.JiraDashboard, LegacyPages.JiraDashboardHtml, LegacyPages.JiraWorklist, LegacyPages.JiraWorklistHtml
    ];

    /// <summary>True when a local address (query and fragment allowed) opens one of the Jira screens, by its new or old address.</summary>
    public static bool IsJiraScreen(string? localUrl)
    {
        if (string.IsNullOrWhiteSpace(localUrl))
        {
            return false;
        }

        var path = localUrl.Split('?', '#')[0].Trim('/');
        return JiraScreenPaths.Any(page => string.Equals(path, page, StringComparison.OrdinalIgnoreCase));
    }

    public static class RouteNames
    {
        public const string DashboardHtml = "dashboard-html";
        public const string ActivityHtml = "activity-html";
        public const string NotesHtml = "notes-html";
        public const string ChecklistHtml = "checklist-html";
        public const string FocusTimerHtml = "focus-timer-html";
        public const string QuickLinksHtml = "quick-links-html";
        public const string PromptTemplatesHtml = "prompt-templates-html";
        public const string ComponentsHtml = "components-html";
        public const string ComponentsTabHtml = "components-tab-html";
        public const string KendoDemoHtml = "kendo-demo-html";
        public const string KendoDemoTabHtml = "kendo-demo-tab-html";
        public const string TabulatorDemoHtml = "tabulator-demo-html";
        public const string JiraDashboardHtml = "jira-dashboard-html";
        public const string JiraWorklistHtml = "jira-worklist-html";
        public const string TextCompareHtml = "text-compare-html";
        public const string SchemaCompareHtml = "schema-compare-html";
        public const string SqlProfilerHtml = "sql-profiler-html";
        public const string PrivacyHtml = "privacy-html";
        public const string SettingsHtml = "settings-html";
        public const string ActivateHtml = "activate-html";
        public const string UpdateHtml = "update-html";
        public const string ErrorHtml = "error-html";
    }

    public static class ComponentsTabs
    {
        public const string Foundations = "foundations";
        public const string TextInput = "text-input";
        public const string Selection = "selection";
        public const string DateTimeNumeric = "date-time-numeric";
        public const string GridData = "grid-data-list";
        public const string FeedbackPopup = "feedback-popup";
        public const string NavigationLayout = "navigation-layout";
        public const string Catalog = "catalog";

        public const string Default = Foundations;

        public static readonly string[] All =
        [
            Foundations,
            TextInput,
            Selection,
            DateTimeNumeric,
            GridData,
            FeedbackPopup,
            NavigationLayout,
            Catalog
        ];

        public static string? Normalize(string? tab)
        {
            if (string.IsNullOrWhiteSpace(tab))
            {
                return Default;
            }

            return All.FirstOrDefault(x => string.Equals(x, tab, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class KendoDemoTabs
    {
        public const string DataSourceListView = "data-source-listview";
        public const string Selection = "selection";
        public const string FormCulture = "form-culture";
        public const string LayoutPopup = "layout-popup";

        public const string Default = DataSourceListView;

        public static readonly string[] All =
        [
            DataSourceListView,
            Selection,
            FormCulture,
            LayoutPopup
        ];

        public static string? Normalize(string? tab)
        {
            if (string.IsNullOrWhiteSpace(tab))
            {
                return Default;
            }

            return All.FirstOrDefault(x => string.Equals(x, tab, StringComparison.OrdinalIgnoreCase));
        }
    }
}
