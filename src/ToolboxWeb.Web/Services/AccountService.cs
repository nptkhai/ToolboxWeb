using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Infrastructure.Storage;

namespace ToolboxWeb.Web.Services;

public interface IAccountService
{
    /// <summary>Deletes the account, every row the user owns and their uploaded profile images.</summary>
    Task<IdentityResult> DeleteAccountAsync(ApplicationUser user);

    /// <summary>The user's personal data as a JSON document (identity fields marked [PersonalData]).</summary>
    Task<byte[]> ExportPersonalDataAsync(ApplicationUser user);
}

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RuntimeStoragePaths _storagePaths;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        RuntimeStoragePaths storagePaths,
        ILogger<AccountService> logger)
    {
        _db = db;
        _userManager = userManager;
        _storagePaths = storagePaths;
        _logger = logger;
    }

    public async Task<IdentityResult> DeleteAccountAsync(ApplicationUser user)
    {
        var userId = user.Id;

        // User-owned tables have no foreign key to AspNetUsers, so deleting the user alone
        // would leave their notes, to-dos and logs behind. One transaction for all of it.
        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Notes.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.ChecklistItems.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.FocusSessions.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.QuickLinks.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.PromptTemplates.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.ActivityLogs.Where(x => x.UserId == userId).ExecuteDeleteAsync();
        await _db.TabulatorTasks.Where(x => x.UserId == userId).ExecuteDeleteAsync();

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            return result;
        }

        await transaction.CommitAsync();
        DeleteProfileImages(userId);
        return result;
    }

    public async Task<byte[]> ExportPersonalDataAsync(ApplicationUser user)
    {
        var personalData = new Dictionary<string, string?>();
        foreach (var property in typeof(ApplicationUser).GetProperties()
                     .Where(p => Attribute.IsDefined(p, typeof(PersonalDataAttribute))))
        {
            personalData[property.Name] = property.GetValue(user)?.ToString();
        }

        foreach (var login in await _userManager.GetLoginsAsync(user))
        {
            personalData[$"{login.LoginProvider} login"] = login.ProviderKey;
        }

        personalData["AuthenticatorKey"] = await _userManager.GetAuthenticatorKeyAsync(user);
        return JsonSerializer.SerializeToUtf8Bytes(personalData, new JsonSerializerOptions { WriteIndented = true });
    }

    private void DeleteProfileImages(string userId)
    {
        // Same folder UserProfileService saves avatars to; the user id is a GUID, never a path.
        var folder = Path.Combine(_storagePaths.UploadsPath, "images", "profile", userId);
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Could not delete the profile images of a deleted account.");
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Could not delete the profile images of a deleted account.");
        }
    }
}
