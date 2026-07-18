using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly IJiraAuthService _jiraAuth;

    public LogoutModel(IJiraAuthService jiraAuth)
    {
        _jiraAuth = jiraAuth;
    }

    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        await _jiraAuth.SignOutAsync(HttpContext);
        if (Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl!);
        }

        return Redirect("~/");
    }
}
