using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Services;

namespace ToolboxWeb.Web.Areas.Identity.Pages.Account.Manage;

/// <summary>
/// Personal data: download it (Identity's list), move all Toolkit data to another copy (export
/// and import, DataTransferService), delete the account.
/// </summary>
[Authorize]
// Downloading and exporting stay possible on an ended desktop license; importing checks the license itself.
[AllowWhenUnlicensed]
[RequestSizeLimit(DataTransferService.MaxFileBytes + 1024 * 1024)]
public partial class PersonalDataModel : ManagePageModel
{
    private readonly IAccountService _accounts;
    private readonly IDataTransferService _transfer;
    private readonly IDeviceLicenseService _licenses;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public PersonalDataModel(
        UserManager<ApplicationUser> userManager,
        ICurrentUserService currentUser,
        IAccountService accounts,
        IDataTransferService transfer,
        IDeviceLicenseService licenses,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
        : base(userManager, currentUser)
    {
        _accounts = accounts;
        _transfer = transfer;
        _licenses = licenses;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    /// <summary>What the uploaded file would add, shown before the import is confirmed.</summary>
    public DataTransferPreview? Preview { get; private set; }

    /// <summary>Names the uploaded file kept on this computer until it is imported.</summary>
    public string? ImportToken { get; private set; }

    public string? ImportError { get; private set; }

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

    /// <summary>All Toolkit data in one file, to import into another copy (the desktop app).</summary>
    public async Task<IActionResult> OnPostExportAsync()
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        return File(await _transfer.ExportAsync(user.Id), "application/json", $"toolkit-data-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>Reads an uploaded file and shows what it would add.</summary>
    public async Task<IActionResult> OnPostPreviewAsync(IFormFile? file)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        if (ReadOnlyLicense() is { } refused)
        {
            ImportError = refused;
            return Page();
        }

        if (file is null || file.Length == 0)
        {
            ImportError = _localizer["DataTransfer.Error.NoFile"].Value;
            return Page();
        }

        if (file.Length > DataTransferService.MaxFileBytes)
        {
            ImportError = _localizer["DataTransfer.Error.TooBig"].Value;
            return Page();
        }

        await using var stream = file.OpenReadStream();
        var (data, error) = _transfer.Read(stream);
        if (data is null)
        {
            ImportError = _localizer[error!].Value;
            return Page();
        }

        // Kept on this computer until "Import", so the file is not uploaded twice.
        var token = Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(ImportFolder(user.Id));
        await using (var saved = System.IO.File.Create(ImportPath(user.Id, token)))
        {
            stream.Position = 0;
            await stream.CopyToAsync(saved);
        }

        ImportToken = token;
        Preview = await _transfer.PreviewAsync(user.Id, data);
        return Page();
    }

    public async Task<IActionResult> OnPostImportAsync(string? token)
    {
        var user = await GetUserAsync();
        if (user is null)
        {
            return UserMissing();
        }

        if (ReadOnlyLicense() is { } refused)
        {
            ImportError = refused;
            return Page();
        }

        if (token is null || !TokenPattern().IsMatch(token) || !System.IO.File.Exists(ImportPath(user.Id, token)))
        {
            ImportError = _localizer["DataTransfer.Error.Expired"].Value;
            return Page();
        }

        var path = ImportPath(user.Id, token);
        DataTransferPreview result;
        try
        {
            await using var stream = System.IO.File.OpenRead(path);
            var (data, error) = _transfer.Read(stream);
            if (data is null)
            {
                ImportError = _localizer[error!].Value;
                return Page();
            }

            result = await _transfer.ImportAsync(user.Id, data);
        }
        finally
        {
            System.IO.File.Delete(path);
        }

        var summary = _localizer["DataTransfer.Imported", Count(result, "Notes"), Count(result, "Checklist"), Count(result, "FocusSessions"),
            Count(result, "QuickLinks"), Count(result, "Prompts")].Value;
        await _activityLog.LogAsync(user.Id, ActivityActionType.Create, ActivityEntityType.UserProfile, null, summary);
        StatusMessage = summary;
        return RedirectToPage();
    }

    private static int Count(DataTransferPreview preview, string kind) => preview.Counts.FirstOrDefault(x => x.Kind == kind)?.New ?? 0;

    // Importing writes app data, so an ended desktop license refuses it like any other write.
    private string? ReadOnlyLicense() =>
        _licenses.GetStatus().IsReadOnly ? _localizer["License.Blocked.Expired"].Value : null;

    private static string ImportFolder(string userId) => Path.Combine(Path.GetTempPath(), "toolkit-import", userId);

    private static string ImportPath(string userId, string token) => Path.Combine(ImportFolder(userId), token + ".json");

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex TokenPattern();
}
