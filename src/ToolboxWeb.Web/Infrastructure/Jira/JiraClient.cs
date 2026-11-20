using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ToolboxWeb.Web.ViewModels.Jira;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Time;

namespace ToolboxWeb.Web.Infrastructure.Jira;

/// <summary>
/// Jira Server / Data Center REST client. Signs in with a password (session cookie) or with a
/// personal access token (Authorization: Bearer). With a password, a request that comes back 401
/// because Jira dropped the session signs in again once and is sent again.
/// </summary>
/// <remarks>
/// Searches read every page (Jira returns at most 1000 issues a page) up to <see cref="MaxIssues"/>,
/// ask only for the fields the screens use, and take worklogs from the search answer: only an
/// issue with more worklogs than the answer holds is read again, a few issues at a time. Times
/// arrive in UTC and are turned into the app time zone (App:TimeZone).
/// </remarks>
public sealed class JiraClient : IJiraClient
{
    /// <summary>The most issues one search reads; the Search tab says so when there are more.</summary>
    public const int MaxIssues = 5000;

    private const int PageSize = 500;
    private const int WorklogReadsAtOnce = 6;
    public const string SubTaskTypeFieldName = "Sub-task Type";

    private readonly string _password;
    private readonly SemaphoreSlim _fieldLock = new(1, 1);
    private string? _subTaskTypeField;
    private bool _fieldsResolved;
    private readonly string? _token;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;

    public JiraClient(string baseUrl, string username, string password)
        : this(baseUrl, username, password, token: null)
    {
    }

    private JiraClient(string baseUrl, string username, string password, string? token)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        Username = username;
        _password = password;
        _token = token;
        _http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
        if (token is not null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>A client that signs in with a personal access token; the username comes from Jira.</summary>
    public static JiraClient WithToken(string baseUrl, string token) => new(baseUrl, string.Empty, string.Empty, token);

    public string BaseUrl { get; }
    public string Username { get; private set; }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null)
        {
            // A token needs no sign-in; checking it also tells whose token it is.
            using var me = JsonDocument.Parse(await ReadSuccessAsync(await _http.GetAsync(Url("/rest/api/2/myself"), cancellationToken)));
            Username = ReadString(me.RootElement, "name") ?? ReadString(me.RootElement, "key") ?? Username;
            return;
        }

        if (!await LoginByGadgetAsync(cancellationToken))
        {
            await LoginBySessionAsync(cancellationToken);
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_token is not null)
        {
            return;
        }

        try
        {
            await _http.PostAsync(Url("/rest/auth/1/session"), null, cancellationToken);
        }
        catch
        {
        }
    }

    public async Task<string> GetCurrentUserDisplayNameAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/myself", cancellationToken));
        var root = doc.RootElement;
        return ReadString(root, "displayName") ?? ReadString(root, "name") ?? Username;
    }

    public async Task<List<JiraFilter>> GetFavouriteFiltersAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/filter/favourite", cancellationToken));
        var result = new List<JiraFilter>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            result.Add(new JiraFilter
            {
                Id = ReadString(item, "id") ?? string.Empty,
                Name = ReadString(item, "name") ?? string.Empty,
                Jql = ReadString(item, "jql") ?? string.Empty
            });
        }

        return result;
    }

    public async Task<List<JiraUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/user/search?username=.&maxResults=1000", cancellationToken));
        var result = new List<JiraUser>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name = ReadString(item, "name") ?? ReadString(item, "key");
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(new JiraUser
                {
                    Name = name,
                    DisplayName = ReadString(item, "displayName") ?? name
                });
            }
        }

        return result;
    }

    public async Task<List<JiraIssue>> SearchIssuesAsync(string jql, bool includeWorklogs = true, CancellationToken cancellationToken = default)
    {
        var subTaskTypeField = await ResolveSubTaskTypeFieldAsync(cancellationToken);
        var fields = "summary,status,timetracking,duedate,created,issuetype,parent"
            + (includeWorklogs ? ",worklog" : string.Empty)
            + (subTaskTypeField is null ? string.Empty : "," + subTaskTypeField);

        var result = new List<JiraIssue>();
        var needMoreWorklogs = new List<JiraIssue>();
        var startAt = 0;
        while (result.Count < MaxIssues)
        {
            var path = "/rest/api/2/search?startAt=" + startAt.ToString(CultureInfo.InvariantCulture)
                + "&maxResults=" + Math.Min(PageSize, MaxIssues - result.Count).ToString(CultureInfo.InvariantCulture)
                + "&fields=" + Uri.EscapeDataString(fields)
                + "&jql=" + Uri.EscapeDataString(jql);
            using var doc = JsonDocument.Parse(await GetStringAsync(path, cancellationToken));
            var root = doc.RootElement;
            if (!root.TryGetProperty("issues", out var issues) || issues.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var item in issues.EnumerateArray())
            {
                var issue = MapIssue(item, subTaskTypeField);
                if (includeWorklogs && ReadInlineWorklogs(item, issue))
                {
                    needMoreWorklogs.Add(issue);
                }

                result.Add(issue);
            }

            startAt += issues.GetArrayLength();
            var total = root.TryGetProperty("total", out var totalValue) && totalValue.TryGetInt32(out var t) ? t : 0;
            if (startAt >= total)
            {
                break;
            }
        }

        if (needMoreWorklogs.Count > 0)
        {
            // The search answer holds the first 20 worklogs of an issue; read the rest a few at a time.
            using var gate = new SemaphoreSlim(WorklogReadsAtOnce);
            await Task.WhenAll(needMoreWorklogs.Select(async issue =>
            {
                await gate.WaitAsync(cancellationToken);
                try
                {
                    issue.Worklogs = await GetWorklogsAsync(issue.IssueKey, cancellationToken);
                }
                catch
                {
                    // Keep the worklogs the search answer carried.
                }
                finally
                {
                    gate.Release();
                }
            }));
        }

        if (includeWorklogs)
        {
            foreach (var issue in result)
            {
                issue.LoggedDates = FormatLoggedDates(issue.Worklogs);
            }
        }

        return result;
    }

    // Fills issue.Worklogs from the "worklog" field of a search answer; true when Jira left some out.
    private static bool ReadInlineWorklogs(JsonElement item, JiraIssue issue)
    {
        if (!item.TryGetProperty("fields", out var fields)
            || !fields.TryGetProperty("worklog", out var worklog)
            || worklog.ValueKind != JsonValueKind.Object
            || !worklog.TryGetProperty("worklogs", out var worklogs))
        {
            return false;
        }

        issue.Worklogs = worklogs.EnumerateArray().Select(MapWorklog).ToList();
        var total = worklog.TryGetProperty("total", out var totalValue) && totalValue.TryGetInt32(out var t) ? t : issue.Worklogs.Count;
        return total > issue.Worklogs.Count;
    }

    private async Task<List<WorklogEntry>> GetWorklogsAsync(string issueKey, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/issue/" + Uri.EscapeDataString(issueKey) + "/worklog", cancellationToken));
        return doc.RootElement.TryGetProperty("worklogs", out var worklogs)
            ? worklogs.EnumerateArray().Select(MapWorklog).ToList()
            : [];
    }

    private static WorklogEntry MapWorklog(JsonElement item)
    {
        var author = item.TryGetProperty("author", out var authorValue) ? authorValue : default;
        return new WorklogEntry
        {
            Started = ParseJiraDate(ReadString(item, "started")) ?? DateTime.MinValue,
            TimeSpent = ReadString(item, "timeSpent") ?? string.Empty,
            Seconds = ReadLong(item, "timeSpentSeconds"),
            AuthorName = ReadString(author, "name")
                ?? ReadString(author, "key")
                ?? ReadString(author, "displayName")
                ?? string.Empty
        };
    }

    // ------------------------------------------------------------------ catalogs

    public async Task<List<JiraCatalogProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/project?expand=lead", cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogProject(
                ReadString(item, "key") ?? string.Empty,
                ReadString(item, "name") ?? string.Empty,
                item.TryGetProperty("projectCategory", out var category) ? ReadString(category, "name") ?? string.Empty : string.Empty,
                ReadString(item, "projectTypeKey") ?? string.Empty,
                item.TryGetProperty("lead", out var lead) ? ReadString(lead, "displayName") ?? ReadString(lead, "name") ?? string.Empty : string.Empty))
            .Where(x => x.Key.Length > 0)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<JiraCatalogStatus>> GetStatusesAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/status", cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogStatus(
                ReadString(item, "id") ?? string.Empty,
                ReadString(item, "name") ?? string.Empty,
                item.TryGetProperty("statusCategory", out var category) ? ReadString(category, "key") ?? string.Empty : string.Empty))
            .Where(x => x.Name.Length > 0)
            .ToList();
    }

    public async Task<List<JiraCatalogIssueType>> GetIssueTypesAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/issuetype", cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogIssueType(
                ReadString(item, "id") ?? string.Empty,
                ReadString(item, "name") ?? string.Empty,
                item.TryGetProperty("subtask", out var subtask) && subtask.ValueKind == JsonValueKind.True))
            .Where(x => x.Name.Length > 0)
            .ToList();
    }

    public Task<List<JiraCatalogItem>> GetPrioritiesAsync(CancellationToken cancellationToken = default) =>
        GetItemsAsync("/rest/api/2/priority", cancellationToken);

    public Task<List<JiraCatalogItem>> GetResolutionsAsync(CancellationToken cancellationToken = default) =>
        GetItemsAsync("/rest/api/2/resolution", cancellationToken);

    private async Task<List<JiraCatalogItem>> GetItemsAsync(string path, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync(path, cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogItem(ReadString(item, "id") ?? string.Empty, ReadString(item, "name") ?? string.Empty))
            .Where(x => x.Name.Length > 0)
            .ToList();
    }

    public async Task<List<JiraCatalogField>> GetFieldsAsync(CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/field", cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogField(
                ReadString(item, "id") ?? string.Empty,
                ReadString(item, "name") ?? string.Empty,
                item.TryGetProperty("custom", out var custom) && custom.ValueKind == JsonValueKind.True,
                item.TryGetProperty("schema", out var schema) ? ReadString(schema, "custom") ?? string.Empty : string.Empty))
            .Where(x => x.Id.Length > 0)
            .ToList();
    }

    public async Task<JiraServerConfig> GetServerConfigAsync(CancellationToken cancellationToken = default)
    {
        using var config = JsonDocument.Parse(await GetStringAsync("/rest/api/2/configuration", cancellationToken));
        using var info = JsonDocument.Parse(await GetStringAsync("/rest/api/2/serverInfo", cancellationToken));
        var time = config.RootElement.TryGetProperty("timeTrackingConfiguration", out var tracking) ? tracking : default;
        return new JiraServerConfig
        {
            HoursPerDay = ReadDecimal(time, "workingHoursPerDay") ?? 8m,
            DaysPerWeek = ReadDecimal(time, "workingDaysPerWeek") ?? 5m,
            TimeFormat = ReadString(time, "timeFormat") ?? string.Empty,
            DefaultUnit = ReadString(time, "defaultUnit") ?? string.Empty,
            Version = ReadString(info.RootElement, "version") ?? string.Empty,
            Title = ReadString(info.RootElement, "serverTitle") ?? string.Empty,
            SubTaskTypeField = await ResolveSubTaskTypeFieldAsync(cancellationToken)
        };
    }

    public async Task<List<string>> GetSubTaskTypesAsync(CancellationToken cancellationToken = default)
    {
        var field = await ResolveSubTaskTypeFieldAsync(cancellationToken);
        if (field is null)
        {
            return [];
        }

        // Values on the user's own subtasks of the last year, and the project and issue type they
        // use most, whose create screen lists every option of the field.
        const string jql = "assignee = currentUser() AND issuetype in subTaskIssueTypes() AND updated >= -365d ORDER BY updated DESC";
        using var doc = JsonDocument.Parse(await GetStringAsync(
            "/rest/api/2/search?maxResults=1000&fields=" + Uri.EscapeDataString("project,issuetype," + field) + "&jql=" + Uri.EscapeDataString(jql),
            cancellationToken));
        var seen = new List<string>();
        var projects = new Dictionary<(string Project, string Type), int>();
        if (doc.RootElement.TryGetProperty("issues", out var issues))
        {
            foreach (var item in issues.EnumerateArray())
            {
                var fields = item.GetProperty("fields");
                var value = ReadOptionValue(fields, field);
                if (value.Length > 0 && !seen.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    seen.Add(value);
                }

                var project = fields.TryGetProperty("project", out var p) ? ReadString(p, "key") : null;
                var type = fields.TryGetProperty("issuetype", out var it) ? ReadString(it, "id") : null;
                if (project is not null && type is not null)
                {
                    projects[(project, type)] = projects.GetValueOrDefault((project, type)) + 1;
                }
            }
        }

        var options = new List<string>();
        if (projects.Count > 0)
        {
            var (projectKey, typeId) = projects.OrderByDescending(x => x.Value).First().Key;
            try
            {
                using var meta = JsonDocument.Parse(await GetStringAsync(
                    "/rest/api/2/issue/createmeta/" + Uri.EscapeDataString(projectKey) + "/issuetypes/" + Uri.EscapeDataString(typeId) + "?maxResults=500",
                    cancellationToken));
                var values = meta.RootElement.TryGetProperty("values", out var v) ? v : default;
                if (values.ValueKind == JsonValueKind.Array)
                {
                    foreach (var fieldMeta in values.EnumerateArray())
                    {
                        if (ReadString(fieldMeta, "fieldId") == field && fieldMeta.TryGetProperty("allowedValues", out var allowed))
                        {
                            options.AddRange(allowed.EnumerateArray()
                                .Select(x => (ReadString(x, "value") ?? string.Empty).Trim())
                                .Where(x => x.Length > 0));
                        }
                    }
                }
            }
            catch
            {
                // An older Jira or a hidden create screen: the values seen on issues still count.
            }
        }

        foreach (var value in seen)
        {
            if (!options.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                options.Add(value);
            }
        }

        return options;
    }

    public async Task<List<JiraCatalogVersion>> GetVersionsAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/project/" + Uri.EscapeDataString(projectKey) + "/versions", cancellationToken));
        return doc.RootElement.EnumerateArray()
            .Select(item => new JiraCatalogVersion(
                ReadString(item, "name") ?? string.Empty,
                item.TryGetProperty("released", out var released) && released.ValueKind == JsonValueKind.True,
                item.TryGetProperty("archived", out var archived) && archived.ValueKind == JsonValueKind.True,
                ReadString(item, "releaseDate")))
            .Where(x => x.Name.Length > 0)
            .ToList();
    }

    // The id of the "Sub-task Type" custom field differs per Jira server; read once per client.
    private async Task<string?> ResolveSubTaskTypeFieldAsync(CancellationToken cancellationToken)
    {
        if (_fieldsResolved)
        {
            return _subTaskTypeField;
        }

        await _fieldLock.WaitAsync(cancellationToken);
        try
        {
            if (!_fieldsResolved)
            {
                try
                {
                    var fields = await GetFieldsAsync(cancellationToken);
                    _subTaskTypeField = fields.FirstOrDefault(x => x.Custom
                        && string.Equals(x.Name.Trim(), SubTaskTypeFieldName, StringComparison.OrdinalIgnoreCase))?.Id;
                }
                catch
                {
                    _subTaskTypeField = null;
                }

                _fieldsResolved = true;
            }

            return _subTaskTypeField;
        }
        finally
        {
            _fieldLock.Release();
        }
    }

    private async Task<bool> LoginByGadgetAsync(CancellationToken cancellationToken)
    {
        var response = await _http.PostAsync(Url("/rest/gadget/1.0/login"), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["os_username"] = Username,
            ["os_password"] = _password,
            ["os_cookie"] = "true"
        }), cancellationToken);

        return response.IsSuccessStatusCode && await ValidateLoginAsync(cancellationToken);
    }

    private async Task LoginBySessionAsync(CancellationToken cancellationToken)
    {
        await PostJsonAsync("/rest/auth/1/session", new { username = Username, password = _password }, cancellationToken);
        if (!await ValidateLoginAsync(cancellationToken))
        {
            throw new InvalidOperationException("Không xác thực được tài khoản Jira.");
        }
    }

    private async Task<bool> ValidateLoginAsync(CancellationToken cancellationToken)
    {
        var response = await _http.GetAsync(Url("/rest/api/2/myself"), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task UpdateDueDateAsync(string issueKey, DateTime? dueDate, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["fields"] = new Dictionary<string, object?>
            {
                ["duedate"] = dueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            }
        };
        var json = JsonSerializer.Serialize(body);
        await ReadSuccessAsync(await SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Put, Url("/rest/api/2/issue/" + Uri.EscapeDataString(issueKey)))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            // Jira Server skips its XSRF check for REST calls that carry this header.
            request.Headers.Add("X-Atlassian-Token", "no-check");
            return request;
        }, cancellationToken));
    }

    private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        return await ReadSuccessAsync(await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, Url(path)), cancellationToken));
    }

    // Sends a request; when Jira answers 401 because its session cookie ran out (idle timeout,
    // Jira restart), signs in again once and resends. A token has no session to renew.
    private async Task<HttpResponseMessage> SendAsync(Func<HttpRequestMessage> create, CancellationToken cancellationToken)
    {
        using (var first = create())
        {
            var response = await _http.SendAsync(first, cancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized || _token is not null)
            {
                return response;
            }

            response.Dispose();
        }

        await LoginAsync(cancellationToken);
        using var retry = create();
        return await _http.SendAsync(retry, cancellationToken);
    }

    private async Task PostJsonAsync(string path, object body, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(body);
        await ReadSuccessAsync(await _http.PostAsync(Url(path), new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken));
    }

    private static async Task<string> ReadSuccessAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException("Jira API lỗi " + (int)response.StatusCode + ": " + body);
        }

        return body;
    }

    private string Url(string path) => BaseUrl + path;

    private static JiraIssue MapIssue(JsonElement item, string? subTaskTypeField)
    {
        var fields = item.GetProperty("fields");
        var parent = fields.TryGetProperty("parent", out var parentValue) && parentValue.ValueKind != JsonValueKind.Null ? parentValue : default;
        var parentFields = parent.ValueKind != JsonValueKind.Undefined && parent.TryGetProperty("fields", out var parentFieldValue)
            ? parentFieldValue
            : default;

        return new JiraIssue
        {
            IssueKey = ReadString(item, "key") ?? string.Empty,
            Summary = ReadString(fields, "summary") ?? string.Empty,
            Parent = parent.ValueKind == JsonValueKind.Undefined ? string.Empty : ReadString(parent, "key") ?? string.Empty,
            ParentSummary = parentFields.ValueKind == JsonValueKind.Undefined ? string.Empty : ReadString(parentFields, "summary") ?? string.Empty,
            Status = ReadNestedName(fields, "status"),
            StatusCategory = fields.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object
                && status.TryGetProperty("statusCategory", out var category)
                ? ReadString(category, "key") ?? string.Empty
                : string.Empty,
            Estimate = ReadTimetracking(fields, "originalEstimate"),
            EstimateSeconds = fields.TryGetProperty("timetracking", out var tracking) ? ReadLong(tracking, "originalEstimateSeconds") : null,
            TimeToLog = ReadTimetracking(fields, "timeSpent"),
            DueDate = ParseDateOnly(ReadString(fields, "duedate")),
            CreateDate = ParseJiraDate(ReadString(fields, "created")),
            Type = ReadNestedName(fields, "issuetype"),
            SubTaskType = subTaskTypeField is null ? string.Empty : ReadOptionValue(fields, subTaskTypeField)
        };
    }

    // A select field's value: {"value": "Developing", …}; some options carry a trailing space.
    private static string ReadOptionValue(JsonElement fields, string fieldId)
    {
        return fields.TryGetProperty(fieldId, out var option) && option.ValueKind == JsonValueKind.Object
            ? (ReadString(option, "value") ?? string.Empty).Trim()
            : string.Empty;
    }

    private static long? ReadLong(JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.TryGetInt64(out var number)
            ? number
            : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.TryGetDecimal(out var number)
            ? number
            : null;
    }

    private static string ReadNestedName(JsonElement fields, string key)
    {
        return fields.TryGetProperty(key, out var obj) ? ReadString(obj, "name") ?? string.Empty : string.Empty;
    }

    private static string ReadTimetracking(JsonElement fields, string key)
    {
        return fields.TryGetProperty("timetracking", out var time) ? ReadString(time, key) ?? string.Empty : string.Empty;
    }

    private static string? ReadString(JsonElement element, string key)
    {
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    private static DateTime? ParseDateOnly(string? value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    // Jira sends "2026-11-18T06:00:00.000+0000" (UTC). The result is the same moment in the app
    // time zone, with no kind, so the day it falls on does not depend on the server's clock.
    private static DateTime? ParseJiraDate(string? value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var moment)
            ? DateTime.SpecifyKind(TimeZoneInfo.ConvertTime(moment, AppClock.TimeZone).DateTime, DateTimeKind.Unspecified)
            : null;
    }

    private static string FormatLoggedDates(IEnumerable<WorklogEntry> worklogs)
    {
        return string.Join(", ", worklogs
            .Where(x => x.Started != DateTime.MinValue)
            .GroupBy(x => x.Started.Date)
            .Select(g => g.Key.ToDisplayDate() + " (" + string.Join(", ", g.Select(x => x.TimeSpent)) + ")"));
    }

    public void Dispose()
    {
        _http.Dispose();
        _fieldLock.Dispose();
    }
}
