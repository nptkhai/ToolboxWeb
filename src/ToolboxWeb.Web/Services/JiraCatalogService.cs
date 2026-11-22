using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// Jira catalogs (projects, statuses, users…): read from Jira only when the user presses "Sync"
/// (or once, automatically, while they are still empty), never on sign-in or page load.
/// </summary>
public interface IJiraCatalogService
{
    /// <summary>The stored catalogs and settings; on the web copy only the settings (the browser keeps the catalogs).</summary>
    Task<JiraCatalogState> GetStateAsync(string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>Reads the given catalogs from Jira, keeps them (not on the web copy) and answers with them.</summary>
    Task<JiraCatalogState> SyncAsync(JiraSession session, JiraCatalogSyncRequest request, CancellationToken cancellationToken = default);

    Task<JiraCatalogSettings> SaveSettingsAsync(string baseUrl, JiraCatalogSettings settings, CancellationToken cancellationToken = default);

    /// <summary>How issues are read for this user: waiting statuses and Jira's hours per day.</summary>
    Task<JiraRules> GetRulesAsync(string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>The synced users, or null when there is no Users catalog on the server (not synced yet, or the web copy).</summary>
    Task<IReadOnlyList<JiraUser>?> GetUsersAsync(string baseUrl, CancellationToken cancellationToken = default);
}

public sealed class JiraCatalogService : IJiraCatalogService
{
    /// <summary>The web copy keeps the waiting statuses in this cookie: it has no database, and the server needs them.</summary>
    public const string WaitingCookie = "toolbox.jira.waiting";

    /// <summary>The web copy keeps Jira's hours per working day (from the Config catalog) in this cookie.</summary>
    public const string HoursPerDayCookie = "toolbox.jira.hoursPerDay";

    private const int MaxVersionProjects = 50;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly AppModeInfo _mode;
    private readonly IHttpContextAccessor _http;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public JiraCatalogService(
        ApplicationDbContext db,
        ICurrentUserService currentUser,
        AppModeInfo mode,
        IHttpContextAccessor http,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _currentUser = currentUser;
        _mode = mode;
        _http = http;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public static string NormalizeBaseUrl(string? baseUrl) => (baseUrl ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();

    public async Task<JiraCatalogState> GetStateAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var key = NormalizeBaseUrl(baseUrl);
        if (_mode.IsWeb)
        {
            return new JiraCatalogState { BaseUrl = key, StoredInBrowser = true, Settings = new JiraCatalogSettings { WaitingStatuses = ReadWaitingCookie() } };
        }

        var rows = await Rows(key).AsNoTracking().ToListAsync(cancellationToken);
        var state = new JiraCatalogState { BaseUrl = key, Settings = ReadSettings(rows) };
        foreach (var row in rows.Where(x => JiraCatalogKinds.IsKnown(x.Kind)))
        {
            state.Catalogs[row.Kind] = new JiraCatalogEntryState { Count = row.ItemCount, SyncedAt = DateTime.SpecifyKind(row.SyncedAt, DateTimeKind.Utc), Data = JsonNode.Parse(row.Data) };
        }

        return state;
    }

    public async Task<JiraCatalogState> SyncAsync(JiraSession session, JiraCatalogSyncRequest request, CancellationToken cancellationToken = default)
    {
        var key = NormalizeBaseUrl(session.BaseUrl);
        var kinds = JiraCatalogKinds.All.Where(k => request.Kinds.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList();
        List<JiraCatalog> stored = _mode.IsWeb ? [] : await Rows(key).ToListAsync(cancellationToken);
        var settings = _mode.IsWeb
            ? new JiraCatalogSettings { WaitingStatuses = ReadWaitingCookie(), VersionProjects = Clean(request.VersionProjects) }
            : ReadSettings(stored);

        var now = DateTime.UtcNow;
        var read = new Dictionary<string, JiraCatalogEntryState>(StringComparer.Ordinal);
        foreach (var kind in kinds)
        {
            try
            {
                var (data, count) = await ReadAsync(session.Client, kind, settings.VersionProjects, cancellationToken);
                read[kind] = new JiraCatalogEntryState { Count = count, SyncedAt = now, Data = data };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                read[kind] = new JiraCatalogEntryState
                {
                    Error = JiraErrorHelper.ToUserMessage(
                        ex,
                        _localizer["JiraCatalog.SyncFailedOne"].Value,
                        _localizer["Jira.SessionExpired"].Value,
                        _localizer["Jira.NotFound"].Value,
                        _localizer["Jira.InvalidRequest"].Value)
                };
            }
        }

        if (_mode.IsWeb)
        {
            if (read.GetValueOrDefault(JiraCatalogKinds.Config) is { Error: null, Data: { } configData }
                && configData.Deserialize<JiraServerConfig>(Json)?.HoursPerDay is > 0 and var hours)
            {
                WriteCookie(HoursPerDayCookie, hours.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return new JiraCatalogState { BaseUrl = key, StoredInBrowser = true, Catalogs = read, Settings = settings };
        }

        foreach (var (kind, entry) in read.Where(x => x.Value.Error is null))
        {
            var row = stored.FirstOrDefault(x => x.Kind == kind);
            if (row is null)
            {
                row = new JiraCatalog { UserId = _currentUser.UserId, BaseUrl = key, Kind = kind };
                _db.JiraCatalogs.Add(row);
            }

            row.Data = entry.Data?.ToJsonString(Json) ?? "[]";
            row.ItemCount = entry.Count;
            row.SyncedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var synced = read.Where(x => x.Value.Error is null).Select(x => _localizer[$"JiraCatalog.Kind.{x.Key}"].Value).ToList();
        if (synced.Count > 0 && !request.First)
        {
            await _activityLog.LogAsync(_currentUser.UserId, ActivityActionType.Update, ActivityEntityType.JiraCatalog, null,
                _localizer["ActivityLog.JiraCatalogSynced", string.Join(", ", synced)].Value);
        }

        var state = await GetStateAsync(key, cancellationToken);
        foreach (var (kind, entry) in read.Where(x => x.Value.Error is not null))
        {
            var old = state.Catalogs.GetValueOrDefault(kind);
            state.Catalogs[kind] = new JiraCatalogEntryState { Count = old?.Count ?? 0, SyncedAt = old?.SyncedAt ?? default, Data = old?.Data, Error = entry.Error };
        }

        return state;
    }

    public async Task<JiraCatalogSettings> SaveSettingsAsync(string baseUrl, JiraCatalogSettings settings, CancellationToken cancellationToken = default)
    {
        var clean = new JiraCatalogSettings
        {
            WaitingStatuses = Clean(settings.WaitingStatuses),
            VersionProjects = Clean(settings.VersionProjects).Take(MaxVersionProjects).ToList()
        };

        if (_mode.IsWeb)
        {
            WriteCookie(WaitingCookie, JsonSerializer.Serialize(clean.WaitingStatuses));
            return clean;
        }

        var key = NormalizeBaseUrl(baseUrl);
        var row = await Rows(key).FirstOrDefaultAsync(x => x.Kind == JiraCatalogKinds.Settings, cancellationToken);
        if (row is null)
        {
            row = new JiraCatalog { UserId = _currentUser.UserId, BaseUrl = key, Kind = JiraCatalogKinds.Settings };
            _db.JiraCatalogs.Add(row);
        }

        row.Data = JsonSerializer.Serialize(clean, Json);
        row.ItemCount = clean.WaitingStatuses.Count;
        row.SyncedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return clean;
    }

    public async Task<JiraRules> GetRulesAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        if (_mode.IsWeb)
        {
            var cookie = _http.HttpContext?.Request.Cookies[HoursPerDayCookie];
            var webHours = decimal.TryParse(cookie, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed is > 0 and <= 24 ? parsed : JiraRules.DefaultHoursPerDay;
            return new JiraRules(webHours, ReadWaitingCookie());
        }

        var key = NormalizeBaseUrl(baseUrl);
        var rows = await Rows(key)
            .Where(x => x.Kind == JiraCatalogKinds.Settings || x.Kind == JiraCatalogKinds.Config)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var hoursPerDay = JiraRules.DefaultHoursPerDay;
        var config = rows.FirstOrDefault(x => x.Kind == JiraCatalogKinds.Config);
        if (config is not null)
        {
            try
            {
                hoursPerDay = JsonSerializer.Deserialize<JiraServerConfig>(config.Data, Json)?.HoursPerDay ?? hoursPerDay;
            }
            catch (JsonException)
            {
                // A damaged row: the default day is still right for most servers.
            }
        }

        return new JiraRules(hoursPerDay, ReadSettings(rows).WaitingStatuses);
    }

    public async Task<IReadOnlyList<JiraUser>?> GetUsersAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        if (_mode.IsWeb)
        {
            return null;
        }

        var key = NormalizeBaseUrl(baseUrl);
        var data = await Rows(key)
            .Where(x => x.Kind == JiraCatalogKinds.Users)
            .Select(x => x.Data)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (data is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<JiraUser>>(data, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void WriteCookie(string name, string value)
    {
        var context = _http.HttpContext;
        context?.Response.Cookies.Append(name, value, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
    }

    private IQueryable<JiraCatalog> Rows(string baseUrl)
    {
        var userId = _currentUser.UserId;
        return _db.JiraCatalogs.Where(x => x.UserId == userId && x.BaseUrl == baseUrl);
    }

    private static async Task<(JsonNode Data, int Count)> ReadAsync(IJiraClient client, string kind, IReadOnlyList<string> versionProjects, CancellationToken ct)
    {
        switch (kind)
        {
            case JiraCatalogKinds.Projects: return Node(await client.GetProjectsAsync(ct));
            case JiraCatalogKinds.Statuses: return Node(await client.GetStatusesAsync(ct));
            case JiraCatalogKinds.IssueTypes: return Node(await client.GetIssueTypesAsync(ct));
            case JiraCatalogKinds.SubTaskTypes: return Node(await client.GetSubTaskTypesAsync(ct));
            case JiraCatalogKinds.Priorities: return Node(await client.GetPrioritiesAsync(ct));
            case JiraCatalogKinds.Resolutions: return Node(await client.GetResolutionsAsync(ct));
            case JiraCatalogKinds.Filters: return Node(await client.GetFavouriteFiltersAsync(ct));
            case JiraCatalogKinds.Users:
                return Node((await client.GetUsersAsync(ct)).OrderBy(x => x.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList());
            case JiraCatalogKinds.Config:
                return (JsonSerializer.SerializeToNode(await client.GetServerConfigAsync(ct), Json)!, 1);
            case JiraCatalogKinds.Versions:
                var versions = new JsonObject();
                var count = 0;
                foreach (var project in versionProjects.Take(MaxVersionProjects))
                {
                    var list = await client.GetVersionsAsync(project, ct);
                    versions[project] = JsonSerializer.SerializeToNode(list, Json);
                    count += list.Count;
                }

                return (versions, count);
            default:
                throw new InvalidOperationException("Unknown catalog " + kind);
        }
    }

    private static (JsonNode Data, int Count) Node<T>(IReadOnlyCollection<T> items) =>
        (JsonSerializer.SerializeToNode(items, Json)!, items.Count);

    private static JiraCatalogSettings ReadSettings(IEnumerable<JiraCatalog> rows)
    {
        var row = rows.FirstOrDefault(x => x.Kind == JiraCatalogKinds.Settings);
        if (row is not null)
        {
            try
            {
                var saved = JsonSerializer.Deserialize<JiraCatalogSettings>(row.Data, Json);
                if (saved is not null)
                {
                    return saved;
                }
            }
            catch (JsonException)
            {
                // Fall back to the defaults below.
            }
        }

        return new JiraCatalogSettings { WaitingStatuses = JiraRules.DefaultWaitingStatuses.ToList() };
    }

    private List<string> ReadWaitingCookie()
    {
        var raw = _http.HttpContext?.Request.Cookies[WaitingCookie];
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                return Clean(JsonSerializer.Deserialize<List<string>>(raw));
            }
            catch (JsonException)
            {
                // A cookie someone edited: use the defaults.
            }
        }

        return JiraRules.DefaultWaitingStatuses.ToList();
    }

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? [])
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => x.Length is > 0 and <= 120)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();
}
