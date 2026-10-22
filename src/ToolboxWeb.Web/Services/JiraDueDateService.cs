using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Writes due dates moved on the Kanban board to Jira (stage 5.3). Each issue is written on its
/// own, so one failure does not stop the others; every success goes into the activity log.
/// </summary>
public interface IJiraDueDateService
{
    Task<IReadOnlyList<JiraDueDateResult>> UpdateAsync(
        IJiraClient client,
        string userId,
        IReadOnlyList<JiraDueDateChange> changes,
        CancellationToken cancellationToken = default);
}

public sealed partial class JiraDueDateService : IJiraDueDateService
{
    /// <summary>One save never writes more than this many issues.</summary>
    public const int MaxChanges = 100;

    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JiraDueDateService(IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<JiraDueDateResult>> UpdateAsync(
        IJiraClient client,
        string userId,
        IReadOnlyList<JiraDueDateChange> changes,
        CancellationToken cancellationToken = default)
    {
        var results = new List<JiraDueDateResult>();
        foreach (var change in changes.Take(MaxChanges))
        {
            var key = change.IssueKey?.Trim() ?? string.Empty;
            if (!IssueKey().IsMatch(key) || !TryParseDate(change.DueDate, out var dueDate))
            {
                results.Add(new JiraDueDateResult(key, false, _localizer["Jira.Save.Invalid"].Value));
                continue;
            }

            try
            {
                await client.UpdateDueDateAsync(key, dueDate, cancellationToken);
                results.Add(new JiraDueDateResult(key, true, null));
                await _activityLog.LogAsync(
                    userId,
                    ActivityActionType.Update,
                    ActivityEntityType.JiraIssue,
                    key,
                    _localizer["ActivityLog.JiraDueDateSummary", key, Describe(change.PreviousDueDate), Describe(change.DueDate)].Value);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new JiraDueDateResult(key, false, JiraErrorHelper.ToUserMessage(
                    ex,
                    _localizer["Jira.Save.Failed"].Value,
                    _localizer["Jira.SessionExpired"].Value,
                    _localizer["Jira.Save.NotFound"].Value,
                    _localizer["Jira.Save.Rejected"].Value)));
            }
        }

        return results;
    }

    private static bool TryParseDate(string? value, out DateTime? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            date = parsed;
            return true;
        }

        return false;
    }

    private string Describe(string? isoDate)
    {
        return TryParseDate(isoDate, out var date) && date.HasValue
            ? date.Value.ToDisplayDate()
            : _localizer["Jira.Save.NoDueDate"].Value;
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]*-\d+$")]
    private static partial Regex IssueKey();
}
