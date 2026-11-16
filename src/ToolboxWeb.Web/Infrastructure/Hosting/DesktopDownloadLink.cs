namespace ToolboxWeb.Web.Infrastructure.Hosting;

/// <summary>
/// The "Download Toolkit for your computer" link on the web and server copies: the license
/// server's download page (App:DesktopDownloadUrl). "{host}" in the setting becomes the host the
/// visitor used, so one value works on localhost, the machine name and its LAN address, for
/// example "https://{host}:8443/download". Not shown on a desktop copy (it is the desktop app) nor
/// on phones and tablets, where the Windows installer cannot run.
/// </summary>
public sealed class DesktopDownloadLink(IConfiguration configuration, AppModeInfo mode)
{
    private static readonly string[] MobileMarkers = ["iPhone", "iPad", "iPod", "Android", "Mobile"];

    /// <summary>The link for this request, or null when it does not apply.</summary>
    public string? For(HttpRequest request)
    {
        var template = configuration["App:DesktopDownloadUrl"];
        if (mode.IsDesktop || string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        var agent = request.Headers.UserAgent.ToString();
        if (MobileMarkers.Any(marker => agent.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        var url = template.Trim().Replace("{host}", request.Host.Host, StringComparison.OrdinalIgnoreCase);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.ToString()
            : null;
    }
}
