using System.Globalization;
using System.Text.RegularExpressions;
using ToolboxWeb.Web.ViewModels.Jira;

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
}

public sealed partial class FakeJiraClient : IJiraClient
{
    private const string SubTaskType = "Sub-task";

    public FakeJiraClient(string baseUrl, string username)
    {
        BaseUrl = baseUrl;
        Username = username;
    }

    public string BaseUrl { get; }
    public string Username { get; }

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
        var issues = BuildSampleIssues(DateTime.Today);

        // A real Jira filters by JQL. Approximate the two filters the screens rely on so that
        // switching period or tab changes the result: subtask-only queries and a date range.
        if (jql.Contains("subTaskIssueTypes", StringComparison.OrdinalIgnoreCase))
        {
            issues = issues.Where(x => x.Type == SubTaskType).ToList();
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
            SubTask("HIUM-2912", "HIUM-2890", "[Khai - P.CNTT] Kiểm tra quyền truy cập báo cáo tổng hợp", null, "In Progress", "2h", Day(-3), Log(Day(-3), "1h"))
        };

        issues.Add(Story("VLUM-900", "[DTNH-KTTA-Tồn đọng] Rà soát danh sách lớp học tồn đọng", "In Developing", Day(4)));
        issues.Add(Story("NTTM-2400", "[NTT] Cập nhật bộ biểu mẫu đào tạo năm học mới", "In Developing", Day(9)));

        foreach (var issue in issues)
        {
            foreach (var log in issue.Worklogs)
            {
                log.AuthorName = Username;
            }

            issue.LoggedDates = string.Join(", ", issue.Worklogs
                .Select(x => x.Started.ToString("dd/MM", CultureInfo.InvariantCulture))
                .Distinct());
        }

        return issues;
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
            CreateDate = (dueDate ?? DateTime.Today).AddDays(-7),
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
}
