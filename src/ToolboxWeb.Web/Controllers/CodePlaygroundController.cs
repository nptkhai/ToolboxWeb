using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Infrastructure.Web;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Controllers;

/// <summary>
/// Code playground: try HTML, CSS and JS. The code runs only in the browser, in a sandboxed
/// frame without access to Toolkit (Content/js/code-playground.js), and the work is kept in the
/// browser, so nothing is posted: the page works on the IIS site, the desktop and the web copy
/// and needs no license check for writing.
/// </summary>
[Authorize]
public class CodePlaygroundController : Controller
{
    private readonly ICurrentUserService _currentUser;
    private readonly IFileVersionProvider _versions;

    public CodePlaygroundController(ICurrentUserService currentUser, IFileVersionProvider versions)
    {
        _currentUser = currentUser;
        _versions = versions;
    }

    [HttpGet("/" + ToolboxRouteSlugs.Pages.CodePlayground)]
    [HttpGet("/" + ToolboxRouteSlugs.HtmlPages.CodePlayground, Name = ToolboxRouteSlugs.RouteNames.CodePlaygroundHtml)]
    public IActionResult Index()
    {
        return View(new CodePlaygroundPageViewModel
        {
            StorageKey = BrowserStorageKey.ForUser("toolbox.playground.v1", _currentUser.UserId),
            FormatterUrl = Versioned("/vendor/js-beautify/js/beautifier.min.js"),
            RunUrl = Url.RouteUrl(ToolboxRouteSlugs.RouteNames.CodePlaygroundRun) ?? "/" + ToolboxRouteSlugs.Pages.CodePlaygroundRun,
            Libraries =
            [
                new("jquery", "jQuery 3.7.1", [Versioned("/lib/jquery/dist/jquery.min.js")]),
                new("bootstrap", "Bootstrap 5.3.3", [
                    Versioned("/lib/bootstrap/dist/css/bootstrap.min.css"),
                    Versioned("/lib/bootstrap/dist/js/bootstrap.bundle.min.js")
                ])
            ]
        });
    }

    /// <summary>"Open in a new tab": an empty sandboxed frame that receives the page from the playground tab.</summary>
    [HttpGet("/" + ToolboxRouteSlugs.Pages.CodePlaygroundRun, Name = ToolboxRouteSlugs.RouteNames.CodePlaygroundRun)]
    [ToolboxWeb.Web.Infrastructure.Web.NoScreenShell]
    public IActionResult Run()
    {
        return View();
    }

    private string Versioned(string path) => _versions.AddFileVersionToPath(Request.PathBase, path);
}
