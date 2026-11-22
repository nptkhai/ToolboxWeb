using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using ToolboxWeb.Web.ViewModels.Jira;
using ToolboxWeb.Web.Extensions;
using ToolboxWeb.Web.Infrastructure.Time;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Infrastructure.Jira;

/// <summary>
/// Development-only Jira client that serves deterministic sample data, so the Jira screens can be
/// opened and checked without a real Jira account. Enabled by Jira:UseFakeData in Development only
/// (see Program.cs). Any base URL, username and password are accepted.
/// </summary>
public sealed class FakeJiraClientFactory : IJiraClientFactory
{
    public IJiraClient Create(string baseUrl, string username, string password)
    {
        return new FakeJiraClient(baseUrl, username);
    }

    // Any token is accepted; it signs in as a fixed sample user.
    public IJiraClient CreateWithToken(string baseUrl, string token)
    {
        return new FakeJiraClient(baseUrl, "sample.token.user");
    }
}

public sealed partial class FakeJiraClient : IJiraClient
{
    private const string SubTaskType = "Sub-task";

    public FakeJiraClient(string baseUrl, string username)
    {
        BaseUrl = baseUrl;
        Username = username;
    }

    // Due dates written through UpdateDueDateAsync, kept for this sign-in so the next search
    // shows them, as a real Jira would.
    private readonly ConcurrentDictionary<string, DateTime?> _dueDates = new(StringComparer.OrdinalIgnoreCase);

    public string BaseUrl { get; }
    public string Username { get; }

    public Task UpdateDueDateAsync(string issueKey, DateTime? dueDate, CancellationToken cancellationToken = default)
    {
        if (!BuildSampleIssues(AppClock.Today).Any(x => string.Equals(x.IssueKey, issueKey, StringComparison.OrdinalIgnoreCase)))
        {
            // Same shape as JiraClient's error, so the page shows the "not found" message.
            throw new InvalidOperationException("Jira API lỗi 404: Issue Does Not Exist");
        }

        _dueDates[issueKey] = dueDate?.Date;
        return Task.CompletedTask;
    }

    public Task LoginAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task LogoutAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<string> GetCurrentUserDisplayNameAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Sample Jira User");
    }

    public Task<List<JiraFilter>> GetFavouriteFiltersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<JiraFilter>
        {
            new() { Id = "10001", Name = "My open subtasks", Jql = "assignee = currentUser() AND statusCategory != Done ORDER BY due ASC" },
            new() { Id = "10002", Name = "Due this week", Jql = "assignee = currentUser() AND due >= startOfWeek() AND due <= endOfWeek()" }
        });
    }

    public Task<List<JiraUser>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new List<JiraUser>
        {
            new() { Name = Username, DisplayName = "Sample Jira User" },
            new() { Name = "an.nguyen", DisplayName = "Nguyễn Văn An" },
            new() { Name = "binh.tran", DisplayName = "Trần Thị Bình" },
            new() { Name = "cuong.le", DisplayName = "Lê Minh Cường" }
        });
    }

    public Task<List<JiraIssue>> SearchIssuesAsync(string jql, bool includeWorklogs = true, CancellationToken cancellationToken = default)
    {
        var issues = BuildSampleIssues(AppClock.Today);

        // A real Jira filters by JQL. Approximate the two filters the screens rely on so that
        // switching period or tab changes the result: subtask-only queries and a date range.
        if (jql.Contains("subTaskIssueTypes", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(x => x.Type == SubTaskType).ToList();
        }

        // Status category and project, as auto-schedule asks for them.
        if (jql.Contains("statusCategory != Done", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(x => x.StatusCategory != "done").ToList();
        }

        if (jql.Contains("statusCategory = \"To Do\"", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(x => x.StatusCategory == "new").ToList();
        }
        else if (jql.Contains("statusCategory = \"In Progress\"", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(x => x.StatusCategory == "indeterminate").ToList();
        }

        var status = StatusInJql().Match(jql);
        if (status.Success)
        {
            issues = issues.Where(x => string.Equals(x.Status, status.Groups[1].Value, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var subTaskType = SubTaskTypeInJql().Match(jql);
        if (subTaskType.Success)
        {
            var wanted = subTaskType.Groups[1].Success ? subTaskType.Groups[1].Value : subTaskType.Groups[2].Value;
            issues = issues.Where(x => string.Equals(x.SubTaskType, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var project = ProjectInJql().Match(jql);
        if (project.Success)
        {
            issues = issues.Where(x => x.IssueKey.StartsWith(project.Groups[1].Value + "-", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var dates = DateInJql().Matches(jql)
            .Select(m => DateTime.ParseExact(m.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToList();
        if (dates.Count >= 2)
        {
            var from = dates.Min();
            var to = dates.Max();
            issues = issues
                .Where(x => (x.DueDate.HasValue && x.DueDate.Value.Date >= from && x.DueDate.Value.Date <= to)
                    || x.Worklogs.Any(w => w.Started.Date >= from && w.Started.Date <= to))
                .ToList();
        }

        if (!includeWorklogs)
        {
            foreach (var issue in issues)
            {
                issue.Worklogs = [];
                issue.LoggedDates = string.Empty;
            }
        }

        return Task.FromResult(issues);
    }

    public void Dispose()
    {
    }

    // ------------------------------------------------------------------ catalogs (sample)

    // Statuses and their Jira categories, as on the real server (report of 2026-11-20).
    private static readonly (string Id, string Name, string Category)[] SampleStatuses =
    [
        ("1", "Open", "new"), ("3", "In Developing", "indeterminate"), ("4", "Reopened", "new"), ("6", "Closed", "done"),
        ("10000", "To Do", "new"), ("10001", "Done", "done"), ("10002", "Under Review", "indeterminate"),
        ("10003", "Approved", "indeterminate"), ("10004", "Cancelled", "done"), ("10005", "Rejected", "done"),
        ("10119", "Review Code", "indeterminate"), ("10122", "In Analysis", "indeterminate"), ("10124", "Review US", "indeterminate"),
        ("10125", "In Testing", "indeterminate"), ("10127", "In Verifying", "indeterminate"), ("10131", "Ready to Develop", "new"),
        ("10132", "Ready to Release", "indeterminate"), ("10133", "Ready To Test", "indeterminate"), ("10135", "Ready To Verify", "indeterminate"),
        ("10136", "In Progress", "indeterminate"), ("10140", "In Following Up", "indeterminate"), ("10200", "IN BE", "indeterminate"),
        ("10201", "IN FE", "indeterminate"), ("10300", "In Review", "indeterminate"),
        ("10400", "Pending", "indeterminate"), ("10401", "Waiting for support", "indeterminate"),
        ("10402", "Waiting for customer", "indeterminate"), ("10403", "Escalated", "indeterminate"),
        ("10404", "Waiting for approval", "indeterminate")
    ];

    private static string CategoryOf(string status) =>
        SampleStatuses.FirstOrDefault(x => string.Equals(x.Name, status, StringComparison.OrdinalIgnoreCase)).Category ?? "new";

    public static readonly string[] SampleSubTaskTypes =
    [
        "Developing", "Supporting", "Meeting", "Release / Deploy", "Annual Leave", "Setup Environment", "Create Document",
        "Fixing bug", "Verify Bug", "Data Processing", "Review Document", "Review Code"
    ];

    public Task<List<JiraCatalogProject>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        static JiraCatalogProject Maint(string key, string school) => new(key, school + ".MAINT", "EMS.MAINT", "business", "Khai T. T.");
        return Task.FromResult(new List<JiraCatalogProject>
        {
            new("ASC", "ASC", "DEPARTMENT", "business", "Nhut B. N."),
            Maint("DNTUM", "DNTU"), Maint("DTHUM", "DTHU"), Maint("GDUM", "GDU"), Maint("HHTM", "HHT"), Maint("HITUM", "HITU"),
            Maint("HIUM", "HIU"), Maint("HUCEM", "HUCE"), Maint("NTTM", "NTT"), Maint("STHCM", "STHC"), Maint("TBUM", "TBU"), Maint("VLUM", "VLU"),
            new("EMSPRO2", "EMS.PRO.V2", "PRODUCT", "software", "Nhut B. N."),
            new("HUFI", "HUFI.CR", "EMS.CR", "business", "Tranh N. H."),
            new("IUH02", "IUH.ONG.02", "EMS.ONGOING", "business", "Tranh N. H."),
            new("MAINTDEPT", "MAINT.DEPT", "DEPARTMENT", "business", "Khai T. T."),
            new("SGDTN", "SGDTN.MAINT", "SCHOOL.PAYMENT", "software", "Nhut B. N."),
            new("PSRL", "PS_RELEASE", string.Empty, "software", "Khai T. T.")
        }.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public Task<List<JiraCatalogStatus>> GetStatusesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleStatuses.Select(x => new JiraCatalogStatus(x.Id, x.Name, x.Category)).ToList());

    public Task<List<JiraCatalogIssueType>> GetIssueTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<JiraCatalogIssueType>
        {
            new("10102", "Bug", false), new("10201", "Build Release", false), new("10202", "Bug after release", false),
            new("10200", "WBS ", false), new("10001", "Story", false), new("10000", "Epic", false), new("10100", "Task", false),
            new("10101", SubTaskType, true), new("10203", "Support", false), new("10400", "Enhancement", false),
            new("10500", "Improvement", false), new("10501", "New Feature", false)
        });

    public Task<List<JiraCatalogItem>> GetPrioritiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<JiraCatalogItem>
        {
            new("1", "Highest"), new("2", "High"), new("3", "Medium"), new("4", "Low"), new("5", "Lowest"), new("10000", "Blocker"), new("10001", "Minor")
        });

    public Task<List<JiraCatalogItem>> GetResolutionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<JiraCatalogItem>
        {
            new("10000", "Done"), new("10001", "Won't Do"), new("10002", "Duplicate"), new("10003", "Cannot Reproduce"), new("10100", "Declined")
        });

    public Task<List<JiraCatalogField>> GetFieldsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new List<JiraCatalogField>
        {
            new("summary", "Summary", false, string.Empty), new("duedate", "Due Date", false, string.Empty),
            new("customfield_10107", "Epic Link", true, "com.pyxis.greenhopper.jira:gh-epic-link"),
            new("customfield_10214", JiraClient.SubTaskTypeFieldName, true, "com.atlassian.jira.plugin.system.customfieldtypes:select"),
            new("customfield_10217", "No charge", true, "com.atlassian.jira.plugin.system.customfieldtypes:multicheckboxes")
        });

    public Task<JiraServerConfig> GetServerConfigAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new JiraServerConfig
        {
            HoursPerDay = 8m,
            DaysPerWeek = 5m,
            TimeFormat = "days",
            DefaultUnit = "hour",
            Version = "9.14.0 (sample)",
            Title = "Sample Jira",
            SubTaskTypeField = "customfield_10214"
        });

    public Task<List<string>> GetSubTaskTypesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SampleSubTaskTypes.ToList());

    public Task<List<JiraCatalogVersion>> GetVersionsAsync(string projectKey, CancellationToken cancellationToken = default)
    {
        var today = AppClock.Today;
        var school = projectKey.EndsWith('M') ? projectKey[..^1] : projectKey;
        return Task.FromResult(Enumerable.Range(0, 4)
            .Select(i => today.AddDays(14 - i * 21))
            .Select(day => new JiraCatalogVersion(
                $"[{school}] {day:dd/MM/yyyy}",
                day <= today,
                false,
                day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .ToList());
    }

    private List<JiraIssue> BuildSampleIssues(DateTime today)
    {
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        DateTime Day(int offset) => monday.AddDays(offset);

        // Parent summaries are often copied into subtasks in the real data, which is why some
        // subtasks below repeat their parent's summary on purpose.
        var issues = new List<JiraIssue>
        {
            SubTask("DNTUM-1076", "DNTUM-1006", "[DNTU] Kiểm tra lỗi cập nhật tổng số tín chỉ bắt buộc trong thiết lập học kỳ trên trang GV", null, "Done", "2h", Day(0), Log(Day(0), "2h")),
            SubTask("HITUM-1518", "HITUM-1509", "[HITU] Đề nghị cho phép hủy kết quả học tập nhiều sinh viên cùng lúc", null, "In Progress", "3h", Day(0), Log(Day(0), "1h"), Log(Day(1), "2h")),
            SubTask("MAINTDEPT-10875", "MAINTDEPT-10871", "[MAINT] [Team] Họp trao đổi công việc", "[MAINT] [Team] Họp trao đổi công việc tuần", "Done", "", Day(0), Log(Day(0), "1h")),
            SubTask("GDUM-1742", "GDUM-1725", "[EDU] Bổ sung cột thông tin \"Email Eoffice\" lên màn hình Quản lý giảng viên", null, "Done", "1h 30m", Day(1), Log(Day(1), "1h 30m")),
            SubTask("GDUM-1743", "GDUM-1738", "[EDU/Quản lý môn học xếp TKB] Bổ sung thông tin Khóa học lên màn hình Kế hoạch mở lớp học phần", null, "Done", "2h", Day(1), Log(Day(1), "2h")),
            SubTask("HITUM-1528", "HITUM-1479", "[HITU] Nhờ dev kiểm tra lỗi ở màn hình E033023 | Đăng ký môn học ngoài chương trình", null, "Done", "1h 30m", Day(1), Log(Day(1), "1h 30m")),
            SubTask("HUCEM-2150", "HUCEM-2101", "[HUCE] Hỗ trợ kiểm tra v/v sao chép lịch học LHP không hiển thị lịch học đã sao chép", null, "Done", "1h 30m", Day(1), Log(Day(1), "1h 30m")),
            SubTask("NTTM-2789", "NTTM-2750", "[NTT] Hỗ trợ release Hrm web, Edu, Egov, Sv, Mes, Hrm_Master", "[NTT] Release định kỳ tháng", "Done", "", Day(2), Log(Day(2), "1h")),
            SubTask("TBUM-468", "TBUM-455", "[TBU] Hỗ trợ kiểm tra lỗi double dữ liệu ở màn hình E080063 | In thời khóa biểu học kỳ", null, "Done", "2h", Day(2), Log(Day(2), "2h")),
            SubTask("TBUM-430", "TBUM-412", "[TBU] Hỗ trợ bổ sung biểu mẫu mới ở màn hình Bổ chấm công lịch dạy", null, "Review Code", "1h", Day(2), Log(Day(2), "1h")),
            SubTask("STHCM-1699", "STHCM-1650", "[STHC] Hỗ trợ hiệu chỉnh mẫu in học bổng", null, "Done", "", Day(2), Log(Day(2), "30m")),
            SubTask("MAINTDEPT-10899", "MAINTDEPT-10871", "[MS] Xin phép vào trễ 1h sáng", "[MAINT] [Team] Họp trao đổi công việc tuần", "Done", "", Day(3), Log(Day(3), "1h")),
            SubTask("VLUM-922", "VLUM-900", "[DTNH-KTTA-Tồn đọng] Kiểm tra lại công thức 3 cột cuối trong danh sách lớp học", null, "In Developing", "4h", Day(4), Log(Day(3), "1h")),
            SubTask("NTTM-2462", "NTTM-2400", "[NTT] Hiệu chỉnh biểu mẫu Chuẩn ra và biểu mẫu Chương trình đào tạo", null, "To Do", "", Day(8)),
            SubTask("HHTM-1115", "HHTM-1100", "[HHT] EDU, KTX - Bổ sung các biến thông tin nội trú các mẫu đề xuất", null, "To Do", "3h", Day(9)),
            SubTask("DTHUM-919", "DTHUM-900", "[DTHU] Hiệu chỉnh biến [ROW_MERGE] khi xuất báo cáo nhiều cấp", null, "To Do", "", null),
            // Overdue: due last week and still open.
            SubTask("HIUM-2912", "HIUM-2890", "[Khai - P.CNTT] Kiểm tra quyền truy cập báo cáo tổng hợp", null, "In Progress", "2h", Day(-3), Log(Day(-3), "1h")),
            // Past due, but waiting on the customer: in progress, never late.
            SubTask("HIUM-2915", "HIUM-2890", "[HIU] Chờ trường xác nhận quyền truy cập báo cáo", null, "In Following Up", "0.25d", Day(-2), Log(Day(-2), "30m")),
            // Cancelled is a "done" status in Jira.
            SubTask("VLUM-930", "VLUM-900", "[VLU] Kiểm tra lại số liệu lớp học (đã hủy)", null, "Cancelled", "1h", Day(1))
        };

        issues.Add(Story("VLUM-900", "[DTNH-KTTA-Tồn đọng] Rà soát danh sách lớp học tồn đọng", "In Developing", Day(4)));
        issues.Add(Story("NTTM-2400", "[NTT] Cập nhật bộ biểu mẫu đào tạo năm học mới", "In Developing", Day(9)));

        foreach (var issue in issues)
        {
            if (_dueDates.TryGetValue(issue.IssueKey, out var due))
            {
                issue.DueDate = due;
            }

            issue.StatusCategory = CategoryOf(issue.Status);
            issue.EstimateSeconds = string.IsNullOrWhiteSpace(issue.Estimate) ? null : (long)(JiraDashboardMetrics.ParseHours(issue.Estimate) * 3600m);
            if (issue.Type == SubTaskType)
            {
                issue.SubTaskType = SampleSubTaskType(issue.Summary);
            }

            foreach (var log in issue.Worklogs)
            {
                log.AuthorName = Username;
                log.Seconds = (long)(JiraDashboardMetrics.ParseHours(log.TimeSpent) * 3600m);
            }

            issue.LoggedDates = string.Join(", ", issue.Worklogs
                .Select(x => x.Started.ToDisplayDate())
                .Distinct());
        }

        return issues;
    }

    // Sub-task Type of the sample subtasks, guessed from the summary like a person would pick it.
    private static string SampleSubTaskType(string summary)
    {
        if (summary.Contains("Họp", StringComparison.OrdinalIgnoreCase)) return "Meeting";
        if (summary.Contains("Xin phép", StringComparison.OrdinalIgnoreCase)) return "Annual Leave";
        if (summary.Contains("release", StringComparison.OrdinalIgnoreCase)) return "Release / Deploy";
        if (summary.Contains("Hỗ trợ", StringComparison.OrdinalIgnoreCase) || summary.Contains("Chờ", StringComparison.OrdinalIgnoreCase)) return "Supporting";
        return "Developing";
    }

    private static JiraIssue SubTask(string key, string parent, string summary, string? parentSummary, string status, string estimate, DateTime? dueDate, params WorklogEntry[] worklogs)
    {
        return new JiraIssue
        {
            IssueKey = key,
            Summary = summary,
            Parent = parent,
            ParentSummary = parentSummary ?? summary,
            Status = status,
            Estimate = estimate,
            TimeToLog = string.Empty,
            DueDate = dueDate,
            CreateDate = (dueDate ?? AppClock.Today).AddDays(-7),
            Type = SubTaskType,
            Worklogs = worklogs.ToList()
        };
    }

    private static JiraIssue Story(string key, string summary, string status, DateTime dueDate)
    {
        return new JiraIssue
        {
            IssueKey = key,
            Summary = summary,
            Status = status,
            DueDate = dueDate,
            CreateDate = dueDate.AddDays(-14),
            Type = "Story"
        };
    }

    private static WorklogEntry Log(DateTime day, string timeSpent)
    {
        return new WorklogEntry { Started = day.AddHours(9), TimeSpent = timeSpent };
    }

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}")]
    private static partial Regex DateInJql();

    [GeneratedRegex("project = \"([^\"]+)\"")]
    private static partial Regex ProjectInJql();

    [GeneratedRegex("status = \"([^\"]+)\"")]
    private static partial Regex StatusInJql();

    [GeneratedRegex("\"Sub-task Type\" = \"([^\"]+)\"|cf\\[\\d+\\] = \"([^\"]+)\"")]
    private static partial Regex SubTaskTypeInJql();
}
