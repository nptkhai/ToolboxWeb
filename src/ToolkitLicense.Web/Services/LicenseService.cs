using Microsoft.EntityFrameworkCore;
using ToolkitLicense.Web.Data;
using ToolkitLicense.Web.Domain;
using ToolkitLicense.Web.Infrastructure;

namespace ToolkitLicense.Web.Services;

public sealed record SubmitResult(ActivationRequest? Request, TokenError Error);

public sealed record ApproveInput(int? CustomerId, string? NewCustomerName, LicenseTerm Term, DateTime? Date, string? Note);

public sealed record ReissueInput(LicenseTerm Term, DateTime? Date);

/// <summary>What a device learns from a heartbeat.</summary>
/// <param name="UpdateRequired">A mandatory release newer than the device's version exists: the device must update before going on.</param>
public sealed record HeartbeatAnswer(string LicenseStatus, string? LicenseId, DateTime? ExpiresAtUtc, string? LicenseText, Release? Latest, bool UpdateAvailable, bool UpdateRequired);

/// <summary>
/// Activation requests, licenses and devices. Every change is written to the audit log in the
/// same save. Licenses are never deleted: revoking or replacing one is recorded on it.
/// </summary>
public sealed class LicenseService(LicenseDbContext db, SigningKey signingKey, IAuditLog audit, ReleaseService releases)
{
    /// <summary>A heartbeat older or newer than this is refused (clock skew, replay).</summary>
    public static readonly TimeSpan HeartbeatWindow = TimeSpan.FromMinutes(15);

    // ------------------------------------------------------------------ activation requests

    /// <summary>
    /// A device's token, sent by the device (online) or pasted by the owner (manual). A device
    /// that already has a pending request gets that one back; one that already holds an active
    /// license gets an approved request carrying it.
    /// </summary>
    public async Task<SubmitResult> SubmitAsync(string? tokenText, ActivationSource source, string? ip)
    {
        var (token, error) = DeviceTokens.Read(tokenText);
        if (token is null)
        {
            return new SubmitResult(null, error);
        }

        var now = DateTime.UtcNow;
        var device = await db.Devices.FindAsync(token.DeviceId);
        if (device is null)
        {
            device = new Device { Id = token.DeviceId, PublicKey = token.PublicKey, FirstSeenAtUtc = now };
            db.Devices.Add(device);
        }
        else if (device.PublicKey != token.PublicKey)
        {
            // The id is taken by another key: a copied data folder or someone guessing ids.
            return new SubmitResult(null, TokenError.KeyMismatch);
        }

        device.MachineName = token.MachineName;
        device.WindowsUser = token.WindowsUser;
        device.AppVersion = string.IsNullOrEmpty(token.AppVersion) ? device.AppVersion : token.AppVersion;
        device.LastSeenAtUtc = now;

        var pending = await db.ActivationRequests
            .Where(x => x.DeviceId == device.Id && x.Status == ActivationStatus.Pending)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync();
        if (pending is not null)
        {
            await db.SaveChangesAsync();
            return new SubmitResult(pending, TokenError.None);
        }

        var request = new ActivationRequest
        {
            DeviceId = device.Id,
            Source = source,
            RequestIp = DeviceTokens.Clip(ip, 64),
            RequestedAtUtc = now
        };

        var active = await ActiveLicenseAsync(device.Id);
        if (active is not null)
        {
            request.Status = ActivationStatus.Approved;
            request.DecidedAtUtc = now;
            request.LicenseId = active.Id;
            request.DecisionNote = "Already licensed";
        }

        db.ActivationRequests.Add(request);
        if (source == ActivationSource.Manual)
        {
            audit.Add(AuditActions.ManualRequest, $"{device.MachineName} ({device.Id})");
        }

        await db.SaveChangesAsync();
        return new SubmitResult(request, TokenError.None);
    }

    public Task<ActivationRequest?> GetRequestAsync(string id) =>
        db.ActivationRequests.Include(x => x.Device).ThenInclude(d => d!.Customer).FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<ActivationRequest>> PendingAsync() =>
        db.ActivationRequests.Include(x => x.Device).Where(x => x.Status == ActivationStatus.Pending)
            .OrderBy(x => x.RequestedAtUtc).ToListAsync();

    public Task<List<ActivationRequest>> HistoryAsync(int take) =>
        db.ActivationRequests.Include(x => x.Device).Where(x => x.Status != ActivationStatus.Pending)
            .OrderByDescending(x => x.DecidedAtUtc).Take(take).ToListAsync();

    public Task<int> PendingCountAsync() => db.ActivationRequests.CountAsync(x => x.Status == ActivationStatus.Pending);

    /// <returns>Null on success, else a localization key.</returns>
    public async Task<string?> ApproveAsync(string requestId, ApproveInput input)
    {
        var request = await GetRequestAsync(requestId);
        if (request?.Device is null || request.Status != ActivationStatus.Pending)
        {
            return "Activation.Error.NotPending";
        }

        var (expires, ok) = LicenseTerms.Resolve(input.Term == LicenseTerm.Keep ? LicenseTerm.None : input.Term, input.Date, null);
        if (!ok)
        {
            return "License.Error.Date";
        }

        var device = request.Device;
        if (!string.IsNullOrWhiteSpace(input.NewCustomerName))
        {
            var customer = new Customer { Name = DeviceTokens.Clip(input.NewCustomerName, 120) };
            db.Customers.Add(customer);
            device.Customer = customer;
            audit.Add(AuditActions.CustomerCreated, customer.Name);
        }
        else if (input.CustomerId is { } customerId)
        {
            device.Customer = await db.Customers.FindAsync(customerId) ?? device.Customer;
        }

        var license = await IssueAsync(device, expires);
        request.Status = ActivationStatus.Approved;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.LicenseId = license.Id;
        request.DecisionNote = DeviceTokens.Clip(input.Note, 500);
        audit.Add(AuditActions.Approved, $"{device.MachineName} ({device.Id}) · {device.Customer?.Name ?? "—"} · {TermText(expires)}");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> RejectAsync(string requestId, string? note)
    {
        var request = await GetRequestAsync(requestId);
        if (request is null || request.Status != ActivationStatus.Pending)
        {
            return "Activation.Error.NotPending";
        }

        request.Status = ActivationStatus.Rejected;
        request.DecidedAtUtc = DateTime.UtcNow;
        request.DecisionNote = DeviceTokens.Clip(note, 500);
        audit.Add(AuditActions.Rejected, $"{request.Device?.MachineName} ({request.DeviceId})");
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>The license text for a polling device: only while the request's license is still the device's active one.</summary>
    public async Task<(ActivationRequest? Request, License? License)> PollAsync(string id)
    {
        var request = await db.ActivationRequests.FirstOrDefaultAsync(x => x.Id == id);
        if (request?.LicenseId is null)
        {
            return (request, null);
        }

        var license = await db.Licenses.FirstOrDefaultAsync(x => x.Id == request.LicenseId);
        if (license is not null && !license.IsActive(DateTime.UtcNow))
        {
            // Renewed since: hand over the license that replaced it.
            license = await ActiveLicenseAsync(request.DeviceId);
        }

        return (request, license);
    }

    // ------------------------------------------------------------------ devices and licenses

    public async Task<List<Device>> DevicesAsync(string? search)
    {
        var query = db.Devices.Include(x => x.Customer).Include(x => x.Licenses).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(x => x.Id.Contains(s) || x.MachineName.Contains(s) || x.WindowsUser.Contains(s)
                || (x.Customer != null && x.Customer.Name.Contains(s)));
        }

        return await query.OrderByDescending(x => x.LastSeenAtUtc).ToListAsync();
    }

    public Task<Device?> DeviceAsync(string id) =>
        db.Devices.Include(x => x.Customer).Include(x => x.Licenses).FirstOrDefaultAsync(x => x.Id == id);

    public Task<List<ActivationRequest>> RequestsForDeviceAsync(string id) =>
        db.ActivationRequests.Where(x => x.DeviceId == id).OrderByDescending(x => x.RequestedAtUtc).ToListAsync();

    public Task<License?> ActiveLicenseAsync(string deviceId)
    {
        var now = DateTime.UtcNow;
        return db.Licenses.Where(x => x.DeviceId == deviceId && x.RevokedAtUtc == null && x.ReplacedByLicenseId == null
                && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .OrderByDescending(x => x.IssuedAtUtc).FirstOrDefaultAsync();
    }

    /// <summary>
    /// A new license for the device (renewal or a lost file): the current one, if any, is marked as
    /// replaced. Allowed on a device whose license was revoked or ran out too.
    /// </summary>
    public async Task<string?> ReissueAsync(string deviceId, ReissueInput input)
    {
        var device = await DeviceAsync(deviceId);
        if (device is null)
        {
            return "Device.Error.NotFound";
        }

        var latest = device.Licenses.OrderByDescending(x => x.IssuedAtUtc).FirstOrDefault();
        var (expires, ok) = LicenseTerms.Resolve(input.Term, input.Date, latest?.ExpiresAtUtc);
        if (!ok || (input.Term == LicenseTerm.Keep && latest is null))
        {
            return input.Term == LicenseTerm.Keep ? "License.Error.KeepExpired" : "License.Error.Date";
        }

        await IssueAsync(device, expires);
        audit.Add(AuditActions.Reissued, $"{device.MachineName} ({device.Id}) · {TermText(expires)}");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> RevokeAsync(string deviceId, string? reason)
    {
        var device = await DeviceAsync(deviceId);
        var license = device is null ? null : await ActiveLicenseAsync(device.Id);
        if (device is null || license is null)
        {
            return "Device.Error.NoActiveLicense";
        }

        license.RevokedAtUtc = DateTime.UtcNow;
        license.RevokeReason = DeviceTokens.Clip(reason, 500);
        audit.Add(AuditActions.Revoked, $"{device.MachineName} ({device.Id}) · {license.RevokeReason}");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> AssignAsync(string deviceId, int? customerId)
    {
        var device = await DeviceAsync(deviceId);
        if (device is null)
        {
            return "Device.Error.NotFound";
        }

        var customer = customerId is null ? null : await db.Customers.FindAsync(customerId.Value);
        device.CustomerId = customer?.Id;
        audit.Add(AuditActions.DeviceAssigned, $"{device.MachineName} ({device.Id}) → {customer?.Name ?? "—"}");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<string?> SetNoteAsync(string deviceId, string? note)
    {
        var device = await db.Devices.FindAsync(deviceId);
        if (device is null)
        {
            return "Device.Error.NotFound";
        }

        device.Note = string.IsNullOrWhiteSpace(note) ? null : DeviceTokens.Clip(note, 500);
        audit.Add(AuditActions.DeviceNote, $"{device.MachineName} ({device.Id})");
        await db.SaveChangesAsync();
        return null;
    }

    public async Task<(int Devices, int Active, int SeenWeek)> CountsAsync()
    {
        var now = DateTime.UtcNow;
        var week = now.AddDays(-7);
        var devices = await db.Devices.CountAsync();
        var active = await db.Licenses.Where(x => x.RevokedAtUtc == null && x.ReplacedByLicenseId == null && (x.ExpiresAtUtc == null || x.ExpiresAtUtc > now))
            .Select(x => x.DeviceId).Distinct().CountAsync();
        var seen = await db.Devices.CountAsync(x => x.LastSeenAtUtc >= week);
        return (devices, active, seen);
    }

    // ------------------------------------------------------------------ heartbeat

    public async Task<(HeartbeatAnswer? Answer, TokenError Error)> HeartbeatAsync(string? text)
    {
        if (!WireFormat.TryDecode(text, WireFormat.HeartbeatPrefix, out var payload, out var signature))
        {
            return (null, TokenError.Malformed);
        }

        var beat = WireFormat.Read<HeartbeatPayload>(payload);
        if (beat is null || beat.V != 1 || !DeviceTokens.IsDeviceId(beat.DeviceId))
        {
            return (null, TokenError.Malformed);
        }

        var device = await db.Devices.FindAsync(beat.DeviceId);
        if (device is null)
        {
            return (null, TokenError.UnknownDevice);
        }

        if (!WireFormat.Verify(device.PublicKey, payload, signature))
        {
            return (null, TokenError.BadSignature);
        }

        var now = DateTime.UtcNow;
        if ((beat.SentAt.UtcDateTime - now).Duration() > HeartbeatWindow)
        {
            return (null, TokenError.Stale);
        }

        device.AppVersion = string.IsNullOrWhiteSpace(beat.AppVersion) ? device.AppVersion : DeviceTokens.Clip(beat.AppVersion, 40);
        device.LastSeenAtUtc = now;
        await db.SaveChangesAsync();

        var active = await ActiveLicenseAsync(device.Id);
        string status;
        if (active is not null)
        {
            status = "active";
        }
        else
        {
            var held = string.IsNullOrEmpty(beat.LicenseId) ? null : await db.Licenses.FirstOrDefaultAsync(x => x.Id == beat.LicenseId && x.DeviceId == device.Id);
            status = held switch
            {
                null => "none",
                { RevokedAtUtc: not null } => "revoked",
                { ExpiresAtUtc: { } end } when end <= now => "expired",
                _ => "none"
            };
        }

        var all = await releases.AllAsync();
        var latest = all.FirstOrDefault();
        var updateAvailable = latest is not null && ReleaseService.Compare(latest.Version, device.AppVersion) > 0;
        var updateRequired = all.Any(x => x.Mandatory && ReleaseService.Compare(x.Version, device.AppVersion) > 0);
        return (new HeartbeatAnswer(
            status,
            active?.Id,
            active?.ExpiresAtUtc,
            // The text only when the device does not hold this license yet (renewed or re-issued).
            active is not null && active.Id != beat.LicenseId ? active.Text : null,
            latest,
            updateAvailable,
            updateRequired), TokenError.None);
    }

    // ------------------------------------------------------------------ signing

    private async Task<License> IssueAsync(Device device, DateTime? expiresAtUtc)
    {
        var now = DateTime.UtcNow;
        var license = new License
        {
            DeviceId = device.Id,
            CustomerId = device.Customer?.Id ?? device.CustomerId,
            Customer = device.Customer,
            IssuedAtUtc = now,
            ExpiresAtUtc = expiresAtUtc
        };
        license.Text = WireFormat.Encode(WireFormat.LicensePrefix, new LicensePayload(
            1,
            license.Id,
            device.Id,
            WireFormat.KeyThumbprint(device.PublicKey),
            device.Customer?.Name ?? string.Empty,
            new DateTimeOffset(now, TimeSpan.Zero),
            expiresAtUtc is null ? null : new DateTimeOffset(expiresAtUtc.Value, TimeSpan.Zero)), signingKey.Sign);

        var current = await ActiveLicenseAsync(device.Id);
        if (current is not null)
        {
            current.ReplacedByLicenseId = license.Id;
        }

        db.Licenses.Add(license);
        return license;
    }

    private static string TermText(DateTime? expiresAtUtc) => expiresAtUtc is null ? "∞" : Display.DateText(expiresAtUtc);
}
