namespace ToolboxWeb.Web.Infrastructure.Excel;

public interface IExcelReportService
{
    Task<byte[]> GenerateAsync<T>(string sheetName, IReadOnlyList<T> rows);
}
