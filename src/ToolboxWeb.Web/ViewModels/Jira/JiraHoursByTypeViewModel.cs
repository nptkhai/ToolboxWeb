namespace ToolboxWeb.Web.ViewModels.Jira;

/// <summary>One line of "Hours by kind of work" on Statistics: a Sub-task Type and the hours logged on it.</summary>
/// <param name="Type">The Sub-task Type; empty for subtasks without one.</param>
public sealed record JiraHoursByTypeViewModel(string Type, decimal Hours, int SubtaskCount);
