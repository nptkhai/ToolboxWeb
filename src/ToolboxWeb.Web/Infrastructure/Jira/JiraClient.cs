using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Infrastructure.Jira;

public sealed class JiraClient : IJiraClient
{
    private readonly string _password;
    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;

    public JiraClient(string baseUrl, string username, string password)
    {
        BaseUrl = baseUrl.TrimEnd('/');
        Username = username;
        _password = password;
        _http = new HttpClient(new HttpClientHandler
        {
            CookieContainer = _cookies,
            UseCookies = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    }

    public string BaseUrl { get; }
    public string Username { get; }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        if (!await LoginByGadgetAsync(cancellationToken))
        {
            await LoginBySessionAsync(cancellationToken);
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
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
        var fields = "summary,status,timetracking,worklog,duedate,created,issuetype,parent";
        var path = "/rest/api/2/search?maxResults=200&fields=" + Uri.EscapeDataString(fields) + "&jql=" + Uri.EscapeDataString(jql);
        using var doc = JsonDocument.Parse(await GetStringAsync(path, cancellationToken));
        var result = new List<JiraIssue>();

        if (!doc.RootElement.TryGetProperty("issues", out var issues))
        {
            return result;
        }

        foreach (var item in issues.EnumerateArray())
        {
            var issue = MapIssue(item);
            if (includeWorklogs)
            {
                try
                {
                    issue.Worklogs = await GetWorklogsAsync(issue.IssueKey, cancellationToken);
                    issue.LoggedDates = FormatLoggedDates(issue.Worklogs);
                }
                catch
                {
                    issue.LoggedDates = string.Empty;
                }
            }

            result.Add(issue);
        }

        return result;
    }

    private async Task<List<WorklogEntry>> GetWorklogsAsync(string issueKey, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(await GetStringAsync("/rest/api/2/issue/" + Uri.EscapeDataString(issueKey) + "/worklog", cancellationToken));
        var result = new List<WorklogEntry>();
        if (!doc.RootElement.TryGetProperty("worklogs", out var worklogs))
        {
            return result;
        }

        foreach (var item in worklogs.EnumerateArray())
        {
            result.Add(new WorklogEntry
            {
                Started = ParseJiraDate(ReadString(item, "started")) ?? DateTime.MinValue,
                TimeSpent = ReadString(item, "timeSpent") ?? string.Empty
            });
        }

        return result;
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

    private async Task<string> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        return await ReadSuccessAsync(await _http.GetAsync(Url(path), cancellationToken));
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

    private static JiraIssue MapIssue(JsonElement item)
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
            Estimate = ReadTimetracking(fields, "originalEstimate"),
            TimeToLog = ReadTimetracking(fields, "timeSpent"),
            DueDate = ParseDateOnly(ReadString(fields, "duedate")),
            CreateDate = ParseJiraDate(ReadString(fields, "created")),
            Type = ReadNestedName(fields, "issuetype")
        };
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
        return element.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    }

    private static DateTime? ParseDateOnly(string? value)
    {
        return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static DateTime? ParseJiraDate(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
            ? date
            : null;
    }

    private static string FormatLoggedDates(IEnumerable<WorklogEntry> worklogs)
    {
        return string.Join(", ", worklogs
            .Where(x => x.Started != DateTime.MinValue)
            .GroupBy(x => x.Started.Date)
            .Select(g => g.Key.ToString("dd/MM/yyyy") + " (" + string.Join(", ", g.Select(x => x.TimeSpent)) + ")"));
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
