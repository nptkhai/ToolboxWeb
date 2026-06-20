namespace ToolboxWeb.Web.Helpers;

public static class FileNameHelper
{
    public static string Sanitize(string fileName)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName.Trim();
    }
}
