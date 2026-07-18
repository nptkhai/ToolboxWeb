using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IUserProfileService
{
    Task<UserProfileManageViewModel?> GetProfileAsync(string userId);
    Task<UserProfileUpdateResult> UpdateAvatarAsync(string userId, IFormFile? avatarFile, bool removeAvatar);
}

public class UserProfileService : IUserProfileService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    };

    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private bool? _avatarColumnExists;

    public UserProfileService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment,
        IActivityLogService activityLog,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _userManager = userManager;
        _environment = environment;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<UserProfileManageViewModel?> GetProfileAsync(string userId)
    {
        if (await HasAvatarColumnAsync())
        {
            return await _userManager.Users
                .AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new UserProfileManageViewModel
                {
                    UserName = x.JiraUsername ?? x.UserName ?? string.Empty,
                    Email = x.Email ?? string.Empty,
                    DisplayName = x.JiraDisplayName ?? x.Email ?? x.UserName ?? string.Empty,
                    AuthSource = x.AuthSource ?? string.Empty,
                    AvatarUrl = x.AvatarUrl
                })
                .SingleOrDefaultAsync();
        }

        return await _userManager.Users
            .AsNoTracking()
            .Where(x => x.Id == userId)
            .Select(x => new UserProfileManageViewModel
            {
                UserName = x.JiraUsername ?? x.UserName ?? string.Empty,
                Email = x.Email ?? string.Empty,
                DisplayName = x.JiraDisplayName ?? x.Email ?? x.UserName ?? string.Empty,
                AuthSource = x.AuthSource ?? string.Empty
            })
            .SingleOrDefaultAsync();
    }

    public async Task<UserProfileUpdateResult> UpdateAvatarAsync(string userId, IFormFile? avatarFile, bool removeAvatar)
    {
        var avatarStorageReady = await HasAvatarColumnAsync();
        var hasNewAvatar = avatarFile is not null && avatarFile.Length > 0;

        if (!avatarStorageReady)
        {
            var userExists = await _userManager.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId);

            if (!userExists)
            {
                return UserProfileUpdateResult.Failure(_localizer["Profile.UserNotFound"].Value);
            }

            if (hasNewAvatar || removeAvatar)
            {
                return UserProfileUpdateResult.Failure(_localizer["Profile.AvatarStoragePending"].Value);
            }

            return UserProfileUpdateResult.Success(false);
        }

        var user = await _userManager.Users.SingleOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return UserProfileUpdateResult.Failure(_localizer["Profile.UserNotFound"].Value);
        }

        if (hasNewAvatar && avatarFile is not null)
        {
            var validationErrors = ValidateAvatarFile(avatarFile);
            if (validationErrors.Count > 0)
            {
                return new UserProfileUpdateResult
                {
                    Succeeded = false,
                    Errors = validationErrors
                };
            }
        }

        var originalAvatarUrl = user.AvatarUrl;
        var hasExistingAvatar = !string.IsNullOrWhiteSpace(originalAvatarUrl);
        var changed = false;
        string? newAvatarUrl = originalAvatarUrl;
        string? savedAbsolutePath = null;

        try
        {
            if (hasNewAvatar && avatarFile is not null)
            {
                (newAvatarUrl, savedAbsolutePath) = await SaveAvatarAsync(userId, avatarFile);
                changed = !string.Equals(newAvatarUrl, originalAvatarUrl, StringComparison.OrdinalIgnoreCase);
            }
            else if (removeAvatar && hasExistingAvatar)
            {
                newAvatarUrl = null;
                changed = true;
            }

            if (!changed)
            {
                return UserProfileUpdateResult.Success(false);
            }

            user.AvatarUrl = newAvatarUrl;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                if (!string.IsNullOrWhiteSpace(savedAbsolutePath) && File.Exists(savedAbsolutePath))
                {
                    File.Delete(savedAbsolutePath);
                }

                return new UserProfileUpdateResult
                {
                    Succeeded = false,
                    Errors = updateResult.Errors.Select(x => x.Description).ToArray()
                };
            }

            if (hasExistingAvatar && (!string.Equals(originalAvatarUrl, newAvatarUrl, StringComparison.OrdinalIgnoreCase) || removeAvatar))
            {
                DeleteAvatarIfManaged(originalAvatarUrl);
            }

            await _activityLog.LogAsync(
                userId,
                ActivityActionType.Update,
                ActivityEntityType.UserProfile,
                userId,
                _localizer["ActivityLog.UserProfileUpdatedSummary"].Value);

            return UserProfileUpdateResult.Success(true);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(savedAbsolutePath) && File.Exists(savedAbsolutePath))
            {
                File.Delete(savedAbsolutePath);
            }

            throw;
        }
    }

    private List<string> ValidateAvatarFile(IFormFile avatarFile)
    {
        var errors = new List<string>();
        var extension = Path.GetExtension(avatarFile.FileName);

        if (!AllowedExtensions.Contains(extension))
        {
            errors.Add(_localizer["Profile.AvatarExtensionInvalid"].Value);
        }

        if (avatarFile.Length > MaxAvatarBytes)
        {
            errors.Add(_localizer["Profile.AvatarSizeInvalid"].Value);
        }

        return errors;
    }

    private async Task<(string RelativeUrl, string AbsolutePath)> SaveAvatarAsync(string userId, IFormFile avatarFile)
    {
        var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "images", "profile", userId);
        Directory.CreateDirectory(uploadsRoot);

        var fileName = $"avatar-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(uploadsRoot, fileName);

        await using var stream = File.Create(absolutePath);
        await avatarFile.CopyToAsync(stream);

        var relativeUrl = $"/uploads/images/profile/{userId}/{fileName}";
        return (relativeUrl, absolutePath);
    }

    private async Task<bool> HasAvatarColumnAsync()
    {
        if (_avatarColumnExists.HasValue)
        {
            return _avatarColumnExists.Value;
        }

        var connection = _db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info('AspNetUsers');";

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var columnName = reader["name"]?.ToString();
                if (string.Equals(columnName, "AvatarUrl", StringComparison.OrdinalIgnoreCase))
                {
                    _avatarColumnExists = true;
                    return true;
                }
            }

            _avatarColumnExists = false;
            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void DeleteAvatarIfManaged(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl))
        {
            return;
        }

        var relativePath = avatarUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var expectedRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, "uploads", "images", "profile"));
        var absolutePath = Path.GetFullPath(Path.Combine(_environment.WebRootPath, relativePath));

        if (!absolutePath.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }
}
