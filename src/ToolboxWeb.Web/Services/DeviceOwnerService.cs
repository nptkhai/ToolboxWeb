using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Infrastructure.Hosting;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// The desktop owner: the one account a desktop copy signs in automatically (App:Mode=Desktop).
/// Its id is kept in <c>device.json</c> (<see cref="DeviceFileStore"/>).
/// </summary>
public interface IDeviceOwnerService
{
    /// <summary>The owner's user id once known; null before the first lookup.</summary>
    string? OwnerId { get; }

    /// <summary>
    /// Finds the owner, in this order: the account recorded in device.json; the only (or else the
    /// oldest) non-Jira account in the database, for a database copied from elsewhere; a new
    /// account named after the Windows user, without a password.
    /// </summary>
    Task<ApplicationUser> GetOrCreateOwnerAsync(CancellationToken cancellationToken = default);
}

public sealed class DeviceOwnerService : IDeviceOwnerService
{
    private const string FallbackUserName = "owner";

    private readonly IServiceScopeFactory _scopes;
    private readonly DeviceFileStore _device;
    private readonly ILogger<DeviceOwnerService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _ownerId;

    public DeviceOwnerService(IServiceScopeFactory scopes, DeviceFileStore device, ILogger<DeviceOwnerService> logger)
    {
        _scopes = scopes;
        _device = device;
        _logger = logger;
    }

    public string? OwnerId => _ownerId;

    public async Task<ApplicationUser> GetOrCreateOwnerAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopes.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var device = _device.Read();
            var knownId = _ownerId ?? device.OwnerId;
            var owner = knownId is null ? null : await users.FindByIdAsync(knownId);

            if (owner is null)
            {
                var candidates = await users.Users
                    .Where(x => x.AuthSource == null || x.AuthSource != AuthSources.Jira)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);
                if (candidates.Count > 1)
                {
                    _logger.LogWarning("The desktop database has {Count} accounts; the oldest one ({UserName}) is used as the owner.", candidates.Count, candidates[0].UserName);
                }

                owner = candidates.FirstOrDefault() ?? await CreateOwnerAsync(users);
            }

            if (device.OwnerId != owner.Id)
            {
                _device.Update(file => file with { OwnerId = owner.Id });
            }

            _ownerId = owner.Id;
            return owner;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ApplicationUser> CreateOwnerAsync(UserManager<ApplicationUser> users)
    {
        var owner = new ApplicationUser
        {
            UserName = OwnerUserName(),
            AuthSource = AuthSources.Local,
            CreatedAt = DateTime.UtcNow
        };
        var result = await users.CreateAsync(owner);
        if (!result.Succeeded)
        {
            // A name already taken (for example by a Jira shadow account): fall back to a fixed one.
            owner.UserName = FallbackUserName + "-" + Guid.NewGuid().ToString("N")[..6];
            result = await users.CreateAsync(owner);
        }

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not create the desktop owner account: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        _logger.LogInformation("Created the desktop owner account {UserName}.", owner.UserName);
        return owner;
    }

    // The Windows account name, reduced to the characters Identity accepts (see Program.cs).
    private static string OwnerUserName()
    {
        var name = new StringBuilder();
        foreach (var ch in Environment.UserName)
        {
            if (char.IsAsciiLetterOrDigit(ch) || "-._@+".Contains(ch))
            {
                name.Append(ch);
            }
        }

        return name.Length > 0 ? name.ToString() : FallbackUserName;
    }
}
