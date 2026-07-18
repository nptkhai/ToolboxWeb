using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Services;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserProfileService _userProfileService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        IUserProfileService userProfileService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userManager = userManager;
        _userProfileService = userProfileService;
        _localizer = localizer;
    }

    public UserProfileManageViewModel Profile { get; private set; } = new();
    public ImageUploadControlViewModel AvatarControl { get; private set; } = new();

    [BindProperty]
    public IFormFile? AvatarFile { get; set; }

    [BindProperty]
    public string? AvatarRemovedState { get; set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound(_localizer["Profile.UserNotFound"].Value);
        }

        await LoadAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return NotFound(_localizer["Profile.UserNotFound"].Value);
        }

        var removeAvatar = HasAvatarRemoval(AvatarRemovedState);
        var result = await _userProfileService.UpdateAvatarAsync(userId, AvatarFile, removeAvatar);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            await LoadAsync(userId);
            return Page();
        }

        StatusMessage = result.Changed
            ? _localizer["Profile.UpdateSuccess"].Value
            : _localizer["Profile.NoChanges"].Value;

        return RedirectToPage();
    }

    private async Task LoadAsync(string userId)
    {
        Profile = await _userProfileService.GetProfileAsync(userId)
            ?? throw new InvalidOperationException(_localizer["Profile.UserNotFound"].Value);

        AvatarControl = new ImageUploadControlViewModel
        {
            InputName = nameof(AvatarFile),
            RemovedInputName = nameof(AvatarRemovedState),
            TriggerText = _localizer["Profile.AvatarTrigger"].Value,
            RemoveText = _localizer["Profile.AvatarRemove"].Value,
            EmptyTitle = _localizer["Profile.AvatarEmptyTitle"].Value,
            EmptyNote = _localizer["Profile.AvatarEmptyNote"].Value,
            HelperText = _localizer["Profile.AvatarHelper"].Value,
            InvalidTypeMessage = _localizer["Profile.AvatarExtensionInvalid"].Value,
            AriaLabel = _localizer["Profile.AvatarAriaLabel"].Value,
            RootCssClass = "tbx-upload-profile-image",
            CurrentItemId = "profile-avatar",
            CurrentImageUrl = Profile.AvatarUrl,
            CurrentFileName = "avatar",
            CurrentFileSize = 0
        };
    }

    private static bool HasAvatarRemoval(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        try
        {
            var removedItems = JsonSerializer.Deserialize<string[]>(payload) ?? [];
            return removedItems.Length > 0;
        }
        catch
        {
            return true;
        }
    }
}
