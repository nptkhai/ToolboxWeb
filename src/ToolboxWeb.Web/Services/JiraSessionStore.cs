using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.ViewModels.Jira;

namespace ToolboxWeb.Web.Services;

public sealed class JiraSessionStore : IJiraSessionStore
{
    private readonly IJiraClientFactory _clientFactory;
    private readonly ConcurrentDictionary<string, JiraSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _activeByUser = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _duration;

    public JiraSessionStore(IJiraClientFactory clientFactory, IOptions<JiraOptions> options)
    {
        _clientFactory = clientFactory;
        _duration = TimeSpan.FromHours(Math.Max(1, options.Value.SessionTimeoutHours));
    }

    public Task<JiraSession?> GetAsync(string browserSessionId)
    {
        if (!_sessions.TryGetValue(browserSessionId, out var session))
        {
            return Task.FromResult<JiraSession?>(null);
        }

        var activeKey = BuildActiveKey(session.BaseUrl, session.Username);
        if (session.ExpiresAt <= DateTime.Now
            || !_activeByUser.TryGetValue(activeKey, out var activeId)
            || activeId != browserSessionId)
        {
            _sessions.TryRemove(browserSessionId, out _);
            return Task.FromResult<JiraSession?>(null);
        }

        return Task.FromResult<JiraSession?>(session);
    }

    public async Task<JiraSession> CreateAsync(string browserSessionId, string baseUrl, string username, string password)
    {
        var activeKey = BuildActiveKey(baseUrl, username);
        if (_activeByUser.TryGetValue(activeKey, out var oldSessionId))
        {
            await InvalidateAsync(oldSessionId);
        }

        var client = _clientFactory.Create(baseUrl, username, password);
        await client.LoginAsync();
        var session = new JiraSession
        {
            BrowserSessionId = browserSessionId,
            BaseUrl = baseUrl,
            Username = username,
            Client = client,
            DisplayName = await client.GetCurrentUserDisplayNameAsync(),
            LoginAt = DateTime.Now,
            ExpiresAt = DateTime.Now.Add(_duration)
        };

        _sessions[browserSessionId] = session;
        _activeByUser[activeKey] = browserSessionId;
        return session;
    }

    public async Task<bool> IsActiveAsync(string browserSessionId)
    {
        return await GetAsync(browserSessionId) is not null;
    }

    public async Task InvalidateAsync(string browserSessionId)
    {
        if (_sessions.TryRemove(browserSessionId, out var session))
        {
            var activeKey = BuildActiveKey(session.BaseUrl, session.Username);
            if (_activeByUser.TryGetValue(activeKey, out var activeId) && activeId == browserSessionId)
            {
                _activeByUser.TryRemove(activeKey, out _);
            }

            await session.Client.LogoutAsync();
            session.Client.Dispose();
        }
    }

    private static string BuildActiveKey(string baseUrl, string username)
    {
        return $"{baseUrl}|{username}".ToLowerInvariant();
    }
}
