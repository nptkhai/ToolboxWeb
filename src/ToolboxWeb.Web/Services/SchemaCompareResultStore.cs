using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using ToolboxWeb.Web.Infrastructure.SqlSchema;
using ToolboxWeb.Web.ViewModels.SchemaCompare;

namespace ToolboxWeb.Web.Services;

public interface ISchemaCompareResultStore
{
    void Save(SchemaCompareStoredResult result);

    /// <summary>Returns the result only when it belongs to the calling browser session.</summary>
    SchemaCompareStoredResult? Get(string resultId, string sessionId);

    TimeSpan Ttl { get; }
}

/// <summary>
/// Holds generated scripts so they can be downloaded after the compare request finished.
/// Deliberately stores no credentials: those never outlive the request that carried them.
/// </summary>
public sealed class SchemaCompareResultStore : ISchemaCompareResultStore
{
    private readonly ConcurrentDictionary<string, SchemaCompareStoredResult> _results = new(StringComparer.Ordinal);

    public SchemaCompareResultStore(IOptions<SqlSchemaOptions> options)
    {
        Ttl = TimeSpan.FromMinutes(Math.Clamp(options.Value.ResultTtlMinutes, 1, 480));
    }

    public TimeSpan Ttl { get; }

    public void Save(SchemaCompareStoredResult result)
    {
        PurgeExpired();
        _results[result.ResultId] = result;
    }

    public SchemaCompareStoredResult? Get(string resultId, string sessionId)
    {
        if (string.IsNullOrEmpty(resultId) || !_results.TryGetValue(resultId, out var result))
        {
            return null;
        }

        if (result.ExpiresAt <= DateTime.Now)
        {
            _results.TryRemove(resultId, out _);
            return null;
        }

        return string.Equals(result.SessionId, sessionId, StringComparison.Ordinal) ? result : null;
    }

    private void PurgeExpired()
    {
        var now = DateTime.Now;
        foreach (var entry in _results)
        {
            if (entry.Value.ExpiresAt <= now)
            {
                _results.TryRemove(entry.Key, out _);
            }
        }
    }
}
