using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;

namespace ToolboxWeb.Web.ViewModels;

/// <summary>The "all activity" page: one page of entries plus the kind filter.</summary>
public class ActivityPageViewModel
{
    public IReadOnlyList<ActivityLog> Items { get; init; } = [];
    public ActivityEntityType? EntityType { get; init; }
    public IReadOnlyList<ActivityEntityType> EntityTypes { get; init; } = [];
    public int Page { get; init; } = 1;
    public int TotalPages { get; init; } = 1;
    public int Total { get; init; }
}
