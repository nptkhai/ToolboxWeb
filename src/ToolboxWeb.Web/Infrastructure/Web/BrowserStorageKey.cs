using System.Security.Cryptography;
using System.Text;

namespace ToolboxWeb.Web.Infrastructure.Web;

/// <summary>
/// localStorage keys for pages that keep the user's work in the browser (Compare text, Code
/// playground): one list per account on the same browser, named by a short hash rather than the
/// account id itself.
/// </summary>
public static class BrowserStorageKey
{
    public static string ForUser(string prefix, string? userId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(userId ?? string.Empty));
        return prefix + ":" + Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
