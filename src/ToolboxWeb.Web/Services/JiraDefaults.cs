namespace ToolboxWeb.Web.Services;

public static class JiraDefaults
{
    public const string DefaultJql =
        "issuetype = Sub-task AND status in (Open, \"In Developing\", Reopened, \"To Do\", \"Under Review\", Approved, \"Review Code\", \"In Analysis\", \"Review US\", \"In Testing\", \"In Verifying\", \"Ready to Develop\", \"Ready to Release\", \"Ready To Test\", \"Ready To Verify\", \"In Progress\", Pending, \"In Following Up\", \"IN BE\", \"IN FE\", \"In Review\", \"Waiting for support\", \"Waiting for customer\", Escalated, \"Waiting for approval\") AND assignee in (currentUser()) AND reporter not in (hattp) ORDER BY due DESC";

    public const string AssignedIssuesJql =
        "assignee in (currentUser()) AND status in (Open, \"In Analysis\") ORDER BY updated DESC";

    public const string DueSubTasksJql =
        "issuetype = Sub-task AND assignee in (currentUser()) AND status in (\"To Do\", \"In Progress\") AND due <= endOfDay() ORDER BY due DESC";
}
