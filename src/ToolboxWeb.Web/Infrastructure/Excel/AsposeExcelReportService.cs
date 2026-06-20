using System.Reflection;
using System.Text;

namespace ToolboxWeb.Web.Infrastructure.Excel;

public class AsposeExcelReportService : IExcelReportService
{
    public Task<byte[]> GenerateAsync<T>(string sheetName, IReadOnlyList<T> rows)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var builder = new StringBuilder();

        builder.AppendLine(string.Join(",", properties.Select(x => Escape(x.Name))));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", properties.Select(x => Escape(x.GetValue(row)?.ToString() ?? string.Empty))));
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
