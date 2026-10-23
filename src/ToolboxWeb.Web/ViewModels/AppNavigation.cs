using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>One page of the app menu.</summary>
/// <param name="GoKey">Second key of the "G then letter" shortcut, or null.</param>
/// <param name="InSidebar">False for pages reached another way (activity, account) that the command palette still lists.</param>
public sealed record AppNavItem(
    string Key,
    string Href,
    string Icon,
    string Label,
    string? GoKey,
    bool InSidebar,
    Func<string, string, bool> IsActive);

public sealed record AppNavGroup(string Key, string Label, IReadOnlyList<AppNavItem> Items);

/// <summary>
/// The single list of the app's pages: the sidebar, the command palette (Ctrl+K) and the
/// "G then letter" shortcuts all read it, so a new screen is added here once and shows up in
/// all three. Who sees which group (Jira user, developer tools) is decided here too.
/// </summary>
public static class AppNavigation
{
    public static IReadOnlyList<AppNavGroup> Build(IUrlHelper url, IStringLocalizer localizer, bool isJiraUser, bool showDevTools)
    {
        string Href(string routeName, string fallback) => url.RouteUrl(routeName) ?? fallback;
        string L(string key) => localizer[key].Value;
        static Func<string, string, bool> On(params string[] controllers) =>
            (controller, _) => controllers.Any(c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase));

        var groups = new List<AppNavGroup>();
        if (isJiraUser)
        {
            static bool IsJiraDashboard(string controller, string action) =>
                string.Equals(controller, "Jira", StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "DashboardPage", StringComparison.OrdinalIgnoreCase);

            groups.Add(new AppNavGroup("jira", L("Nav.Group.Jira"),
            [
                new("jira-dashboard", Href(ToolboxRouteSlugs.RouteNames.JiraDashboardHtml, "/jira-dashboard.html"), "gauge", L("Nav.JiraDashboard"), "j", true, IsJiraDashboard),
                new("jira-worklist", Href(ToolboxRouteSlugs.RouteNames.JiraWorklistHtml, "/jira-worklist.html"), "kanban", L("Nav.JiraWorklist"), "w", true,
                    (controller, action) => string.Equals(controller, "Jira", StringComparison.OrdinalIgnoreCase) && !IsJiraDashboard(controller, action))
            ]));
        }

        groups.Add(new AppNavGroup("personal", L("Nav.Group.Personal"),
        [
            new("dashboard", Href(ToolboxRouteSlugs.RouteNames.DashboardHtml, "/dashboard.html"), "layout-dashboard", L("Nav.Dashboard"), "d", true, On("Dashboard", "Activity")),
            new("notes", Href(ToolboxRouteSlugs.RouteNames.NotesHtml, "/notes.html"), "sticky-note", L("Nav.Notes"), "n", true, On("Notes")),
            new("checklist", Href(ToolboxRouteSlugs.RouteNames.ChecklistHtml, "/checklist.html"), "list-checks", L("Nav.Checklist"), "t", true, On("Checklist")),
            new("focus-timer", Href(ToolboxRouteSlugs.RouteNames.FocusTimerHtml, "/focus-timer.html"), "timer", L("Nav.FocusTimer"), "f", true, On("FocusTimer")),
            new("quick-links", Href(ToolboxRouteSlugs.RouteNames.QuickLinksHtml, "/quick-links.html"), "link", L("Nav.QuickLinks"), "l", true, On("QuickLinks")),
            new("prompts", Href(ToolboxRouteSlugs.RouteNames.PromptTemplatesHtml, "/prompt-templates.html"), "message-square-text", L("Nav.PromptTemplates"), "p", true, On("PromptTemplates")),
            new("activity", Href(ToolboxRouteSlugs.RouteNames.ActivityHtml, "/activity.html"), "activity", L("Activity.Title"), "a", false, On("Activity"))
        ]));

        groups.Add(new AppNavGroup("database", L("Nav.Group.Database"),
        [
            new("schema-compare", Href(ToolboxRouteSlugs.RouteNames.SchemaCompareHtml, "/schema-compare.html"), "git-compare", L("Nav.SchemaCompare"), "s", true, On("SchemaCompare")),
            new("sql-profiler", Href(ToolboxRouteSlugs.RouteNames.SqlProfilerHtml, "/sql-profiler.html"), "activity", L("Nav.SqlProfiler"), "q", true, On("SqlProfiler"))
        ]));

        if (showDevTools)
        {
            groups.Add(new AppNavGroup("dev", L("Nav.Group.Dev"),
            [
                new("components", Href(ToolboxRouteSlugs.RouteNames.ComponentsHtml, "/component.html"), "blocks", L("Nav.Components"), "c", true, On("Components", "DemoKendo", "TabulatorDemo")),
                new("kendo-demo", Href(ToolboxRouteSlugs.RouteNames.KendoDemoHtml, "/demo-kendo.html"), "layout-grid", L("Nav.KendoDemo"), null, false, On("DemoKendo")),
                new("tabulator-demo", Href(ToolboxRouteSlugs.RouteNames.TabulatorDemoHtml, "/tabulator-demo.html"), "table", L("Nav.TabulatorDemo"), null, false, On("TabulatorDemo"))
            ]));
        }

        groups.Add(new AppNavGroup("account", L("Nav.Group.Account"),
        [
            new("account", url.Page("/Account/Manage/Index", new { area = "Identity" }) ?? "/Identity/Account/Manage", "user", L("Auth.ManageAccount"), null, false, (_, _) => false)
        ]));

        return groups;
    }
}
