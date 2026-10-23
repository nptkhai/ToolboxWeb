using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ToolboxWeb.Web.Infrastructure.Bookmarks;

/// <summary>One bookmark read from, or written to, a browser bookmark file.</summary>
public sealed record BookmarkEntry(string Title, string Url, string? Folder);

/// <summary>
/// The "Netscape bookmark file" every major browser exports and imports (Chrome, Edge,
/// Firefox, Safari): nested <c>&lt;DL&gt;</c> lists where <c>&lt;H3&gt;</c> names a folder and
/// <c>&lt;A HREF&gt;</c> is a bookmark. Only the innermost folder name is kept, because quick
/// links have one level of groups.
/// </summary>
public static partial class BookmarkHtml
{
    public static IReadOnlyList<BookmarkEntry> Parse(string html)
    {
        var entries = new List<BookmarkEntry>();
        var folders = new Stack<string?>();
        string? pendingFolder = null;

        foreach (Match token in TokenPattern().Matches(html))
        {
            if (token.Groups["folder"].Success)
            {
                pendingFolder = Clean(token.Groups["folder"].Value);
            }
            else if (token.Groups["open"].Success)
            {
                folders.Push(pendingFolder);
                pendingFolder = null;
            }
            else if (token.Groups["close"].Success)
            {
                if (folders.Count > 0)
                {
                    folders.Pop();
                }
            }
            else if (token.Groups["href"].Success)
            {
                var url = WebUtility.HtmlDecode(token.Groups["href"].Value).Trim();
                var title = Clean(token.Groups["title"].Value);
                var folder = folders.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                entries.Add(new BookmarkEntry(string.IsNullOrWhiteSpace(title) ? url : title, url, folder));
            }
        }

        return entries;
    }

    /// <summary>Folders at the top level, so importing the file back gives the same groups.</summary>
    public static string Write(IEnumerable<BookmarkEntry> entries)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
        html.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
        html.AppendLine("<TITLE>Bookmarks</TITLE>");
        html.AppendLine("<H1>Bookmarks</H1>");
        html.AppendLine("<DL><p>");

        foreach (var group in entries.GroupBy(x => x.Folder ?? string.Empty).OrderBy(x => x.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var indent = "    ";
            if (group.Key.Length > 0)
            {
                html.Append(indent).Append("<DT><H3>").Append(WebUtility.HtmlEncode(group.Key)).AppendLine("</H3>");
                html.Append(indent).AppendLine("<DL><p>");
                indent += "    ";
            }

            foreach (var entry in group)
            {
                html.Append(indent).Append("<DT><A HREF=\"").Append(WebUtility.HtmlEncode(entry.Url)).Append("\">")
                    .Append(WebUtility.HtmlEncode(entry.Title)).AppendLine("</A>");
            }

            if (group.Key.Length > 0)
            {
                html.AppendLine("    </DL><p>");
            }
        }

        html.AppendLine("</DL><p>");
        return html.ToString();
    }

    private static string Clean(string value)
    {
        return WebUtility.HtmlDecode(TagPattern().Replace(value, string.Empty)).Trim();
    }

    [GeneratedRegex(@"<H3[^>]*>(?<folder>.*?)</H3>|(?<open><DL[^>]*>)|(?<close></DL\s*>)|<A\s[^>]*?HREF\s*=\s*""(?<href>[^""]*)""[^>]*>(?<title>.*?)</A>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();
}
