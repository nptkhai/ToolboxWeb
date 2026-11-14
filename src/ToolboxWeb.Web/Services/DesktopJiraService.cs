using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Jira;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

public enum JiraSignInKind
{
    /// <summary>Jira username and password (session cookie).</summary>
    Password,

    /// <summary>Personal access token (Jira Server / Data Center 8.14+).</summary>
    Token
}

/// <summary>What the settings form sends. An empty secret keeps the saved one.</summary>
public sealed record DesktopJiraInput(string? BaseUrl, JiraSignInKind Kind, string? Username, string? Secret);

public sealed record DesktopJiraResult(bool Succeeded, string? Message, string? DisplayName = null, string? Username = null);

/// <summary>The saved connection as the settings page shows it. Never carries the secret.</summary>
public sealed record DesktopJiraStatus(
    bool Configured,
    bool Connected,
    string? BaseUrl,
    JiraSignInKind Kind,
    string? Username,
    string? DisplayName,
    DateTime? VerifiedAtUtc,
    string? LastError);

/// <summary>
/// The desktop's Jira connection (App:Mode=Desktop): saved once on the settings page, then used by
/// every Jira screen. One session for the whole app; when it is missing or has run out it is
/// opened again from the saved connection, so the owner never signs in to Jira by hand.
/// </summary>
public interface IDesktopJiraService
{
    bool IsConfigured { get; }

    DesktopJiraStatus GetStatus();

    /// <summary>The open session, connecting first when needed; null when not configured or the connection fails (see <see cref="GetStatus"/>).</summary>
    Task<JiraSession?> GetSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Tries the form's values (or the saved secret when the form leaves it empty) without saving.</summary>
    Task<DesktopJiraResult> TestAsync(DesktopJiraInput input, CancellationToken cancellationToken = default);

    /// <summary>Tries the values and saves them when Jira accepts them.</summary>
    Task<DesktopJiraResult> SaveAsync(DesktopJiraInput input, CancellationToken cancellationToken = default);

    /// <summary>Forgets the saved connection and closes the session.</summary>
    Task RemoveAsync();

    /// <summary>Closes the session; the next Jira request opens a new one.</summary>
    Task DropSessionAsync();
}

public sealed class DesktopJiraService : IDesktopJiraService
{
    private const string SecretPurpose = "Toolkit.Desktop.JiraSecret.v1";

    private readonly DeviceFileStore _device;
    private readonly IJiraClientFactory _clients;
    private readonly IDataProtector _protector;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<DesktopJiraService> _logger;
    private readonly TimeSpan _sessionLength;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private JiraSession? _session;
    private string? _lastError;

    public DesktopJiraService(
        DeviceFileStore device,
        IJiraClientFactory clients,
        IDataProtectionProvider protection,
        IOptions<JiraOptions> options,
        IStringLocalizer<SharedResource> localizer,
        ILogger<DesktopJiraService> logger)
    {
        _device = device;
        _clients = clients;
        _protector = protection.CreateProtector(SecretPurpose);
        _localizer = localizer;
        _logger = logger;
        _sessionLength = TimeSpan.FromHours(Math.Max(1, options.Value.SessionTimeoutHours));
    }

    public bool IsConfigured => _device.Read().Jira is not null;

    public DesktopJiraStatus GetStatus()
    {
        var saved = _device.Read().Jira;
        var session = _session;
        return new DesktopJiraStatus(
            saved is not null,
            saved is not null && session is not null && session.ExpiresAt > DateTime.Now,
            saved?.BaseUrl,
            ParseKind(saved?.Kind),
            session?.Username ?? saved?.Username,
            session?.DisplayName ?? saved?.DisplayName,
            saved?.VerifiedAtUtc,
            saved is null ? null : _lastError);
    }

    public async Task<JiraSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        var current = _session;
        if (current is not null && current.ExpiresAt > DateTime.Now)
        {
            return current;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_session is not null && _session.ExpiresAt > DateTime.Now)
            {
                return _session;
            }

            await CloseSessionAsync();
            var saved = _device.Read().Jira;
            if (saved is null)
            {
                return null;
            }

            string secret;
            try
            {
                secret = _protector.Unprotect(saved.ProtectedSecret);
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                // The keys of this data folder changed (copied from another computer): enter it again.
                _logger.LogWarning(ex, "The saved Jira password or token could not be decrypted.");
                _lastError = _localizer["Settings.Jira.SecretUnreadable"].Value;
                return null;
            }

            try
            {
                _session = await OpenAsync(saved.BaseUrl, ParseKind(saved.Kind), saved.Username, secret, cancellationToken);
                _lastError = null;
                if (_session.DisplayName != saved.DisplayName)
                {
                    _device.Update(file => file.Jira is null ? file : file with { Jira = file.Jira with { DisplayName = _session.DisplayName } });
                }

                return _session;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Could not connect to Jira at {BaseUrl}.", saved.BaseUrl);
                _lastError = ToMessage(ex);
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<DesktopJiraResult> TestAsync(DesktopJiraInput input, CancellationToken cancellationToken = default)
    {
        var (prepared, error) = Prepare(input);
        if (prepared is null)
        {
            return new DesktopJiraResult(false, error);
        }

        try
        {
            var session = await OpenAsync(prepared.BaseUrl, prepared.Kind, prepared.Username, prepared.Secret, cancellationToken);
            await session.Client.LogoutAsync(cancellationToken);
            session.Client.Dispose();
            return new DesktopJiraResult(true, _localizer["Settings.Jira.TestOk", session.DisplayName].Value, session.DisplayName, session.Username);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DesktopJiraResult(false, ToMessage(ex));
        }
    }

    public async Task<DesktopJiraResult> SaveAsync(DesktopJiraInput input, CancellationToken cancellationToken = default)
    {
        var (prepared, error) = Prepare(input);
        if (prepared is null)
        {
            return new DesktopJiraResult(false, error);
        }

        JiraSession session;
        try
        {
            session = await OpenAsync(prepared.BaseUrl, prepared.Kind, prepared.Username, prepared.Secret, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DesktopJiraResult(false, ToMessage(ex));
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await CloseSessionAsync();
            _device.Update(file => file with
            {
                Jira = new DeviceJiraSettings(
                    prepared.BaseUrl,
                    prepared.Kind.ToString(),
                    session.Username,
                    _protector.Protect(prepared.Secret),
                    session.DisplayName,
                    DateTime.UtcNow)
            });
            _session = session;
            _lastError = null;
        }
        finally
        {
            _gate.Release();
        }

        return new DesktopJiraResult(true, _localizer["Settings.Jira.Saved", session.DisplayName].Value, session.DisplayName, session.Username);
    }

    public async Task RemoveAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseSessionAsync();
            _device.Update(file => file with { Jira = null });
            _lastError = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DropSessionAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await CloseSessionAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<JiraSession> OpenAsync(string baseUrl, JiraSignInKind kind, string? username, string secret, CancellationToken cancellationToken)
    {
        var client = kind == JiraSignInKind.Token
            ? _clients.CreateWithToken(baseUrl, secret)
            : _clients.Create(baseUrl, username ?? string.Empty, secret);
        try
        {
            await client.LoginAsync(cancellationToken);
            var displayName = await client.GetCurrentUserDisplayNameAsync(cancellationToken);
            return new JiraSession
            {
                BrowserSessionId = "desktop",
                BaseUrl = baseUrl,
                // With a token, the username is whatever Jira says the token belongs to.
                Username = string.IsNullOrWhiteSpace(client.Username) ? username ?? string.Empty : client.Username,
                Client = client,
                DisplayName = displayName,
                LoginAt = DateTime.Now,
                ExpiresAt = DateTime.Now.Add(_sessionLength)
            };
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private async Task CloseSessionAsync()
    {
        var session = _session;
        _session = null;
        if (session is not null)
        {
            await session.Client.LogoutAsync();
            session.Client.Dispose();
        }
    }

    // Checks the form and fills in the saved secret when the form leaves it empty and nothing
    // that the secret belongs to (address, sign-in kind, username) has changed.
    private (Prepared? Value, string? Error) Prepare(DesktopJiraInput input)
    {
        string baseUrl;
        try
        {
            baseUrl = JiraAuthService.NormalizeBaseUrl(input.BaseUrl ?? string.Empty);
        }
        catch (InvalidOperationException)
        {
            return (null, _localizer["Settings.Jira.UrlInvalid"].Value);
        }

        if (!baseUrl.StartsWith("http://", StringComparison.Ordinal) && !baseUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            return (null, _localizer["Settings.Jira.UrlInvalid"].Value);
        }

        string? username = null;
        if (input.Kind == JiraSignInKind.Password)
        {
            if (string.IsNullOrWhiteSpace(input.Username))
            {
                return (null, _localizer["Settings.Jira.UsernameRequired"].Value);
            }

            username = JiraAuthService.NormalizeJiraUsername(input.Username);
        }

        var secret = input.Secret;
        if (string.IsNullOrEmpty(secret))
        {
            var saved = _device.Read().Jira;
            var sameConnection = saved is not null
                && saved.BaseUrl == baseUrl
                && ParseKind(saved.Kind) == input.Kind
                && (input.Kind == JiraSignInKind.Token || string.Equals(saved.Username, username, StringComparison.OrdinalIgnoreCase));
            if (!sameConnection)
            {
                return (null, _localizer[input.Kind == JiraSignInKind.Token ? "Settings.Jira.TokenRequired" : "Settings.Jira.PasswordRequired"].Value);
            }

            try
            {
                secret = _protector.Unprotect(saved!.ProtectedSecret);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                return (null, _localizer["Settings.Jira.SecretUnreadable"].Value);
            }
        }

        return (new Prepared(baseUrl, input.Kind, username, secret), null);
    }

    private string ToMessage(Exception ex)
    {
        if (ex is HttpRequestException or TaskCanceledException)
        {
            return _localizer["Settings.Jira.Unreachable"].Value;
        }

        return JiraErrorHelper.ToUserMessage(
            ex,
            _localizer["Jira.LoginFailed"].Value,
            _localizer["Settings.Jira.Rejected"].Value,
            _localizer["Settings.Jira.NotJira"].Value,
            _localizer["Jira.InvalidRequest"].Value);
    }

    private static JiraSignInKind ParseKind(string? kind) =>
        Enum.TryParse<JiraSignInKind>(kind, ignoreCase: true, out var parsed) ? parsed : JiraSignInKind.Password;

    private sealed record Prepared(string BaseUrl, JiraSignInKind Kind, string? Username, string Secret);
}
