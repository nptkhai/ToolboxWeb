using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>Shows freshly generated recovery codes once (they arrive through TempData).</summary>
[Authorize]
public class ShowRecoveryCodesModel : PageModel
{
    [TempData]
    public string[]? RecoveryCodes { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet()
        => RecoveryCodes is null || RecoveryCodes.Length == 0 ? RedirectToPage("./TwoFactorAuthentication") : Page();
}
