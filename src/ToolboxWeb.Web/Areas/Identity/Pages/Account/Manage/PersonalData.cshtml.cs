using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class PersonalDataModel : ManagePageModel
{
    private readonly IAccountService _accounts;

    public PersonalDataModel(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser, IAccountService accounts)
        : base(userManager, currentUser)
    {
        _accounts = accounts;
    }

    public async Task<IActionResult> OnGetAsync() => await GetUserAsync() is null ? UserMissing() : Page();

    /// <summary>Downloads the personal data as JSON.</summary>
    public async Task<IActionResult> OnPostDownloadAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        return File(await _accounts.ExportPersonalDataAsync(user), "application/json", "PersonalData.json");
    }
}
