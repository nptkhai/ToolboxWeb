using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace ToolboxWeb.Web.Localization;

public class JsonLocalizationStore
{
    private const string DefaultFallbackCulture = "en-US";
    private const string ResourcesPath = "Localization";

    private readonly ConcurrentDictionary<string, CultureCacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _resourcesDirectory;

    public JsonLocalizationStore(IWebHostEnvironment environment)
    {
        _resourcesDirectory = Path.Combine(environment.ContentRootPath, ResourcesPath);
    }

    public string SearchedLocation => _resourcesDirectory;

    public (string Value, bool Found) GetString(string cultureName, string key)
    {
        foreach (var culture in GetCultureSearchOrder(cultureName))
        {
            var values = GetCultureValues(culture);
            if (values.TryGetValue(key, out var value))
            {
                return (value, true);
            }
        }

        return (key, false);
    }

    public IEnumerable<KeyValuePair<string, string>> GetAllStrings(string cultureName, bool includeParentCultures)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cultures = includeParentCultures
            ? GetCultureSearchOrder(cultureName).Reverse()
            : [cultureName];

        foreach (var culture in cultures)
        {
            foreach (var item in GetCultureValues(culture))
            {
                result[item.Key] = item.Value;
            }
        }

        return result.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    private IReadOnlyDictionary<string, string> GetCultureValues(string cultureName)
    {
        var filePath = Path.Combine(_resourcesDirectory, $"{cultureName}.json");
        var fileInfo = new FileInfo(filePath);
        var lastWriteUtc = fileInfo.Exists ? fileInfo.LastWriteTimeUtc : DateTime.MinValue;

        var cached = _cache.GetOrAdd(cultureName, _ => SafeLoadCultureFile(fileInfo, lastWriteUtc));
        if (cached.LastWriteUtc == lastWriteUtc)
        {
            return cached.Values;
        }

        var refreshed = SafeLoadCultureFile(fileInfo, lastWriteUtc, cached);
        _cache[cultureName] = refreshed;
        return refreshed.Values;
    }

    private static CultureCacheEntry SafeLoadCultureFile(FileInfo fileInfo, DateTime lastWriteUtc, CultureCacheEntry? fallback = null)
    {
        try
        {
            if (!fileInfo.Exists)
            {
                return new CultureCacheEntry(lastWriteUtc, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            using var stream = fileInfo.OpenRead();
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
            return new CultureCacheEntry(lastWriteUtc, new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase));
        }
        catch (Exception) when (fallback is not null)
        {
            return fallback;
        }
        catch (Exception)
        {
            return new CultureCacheEntry(lastWriteUtc, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> GetCultureSearchOrder(string cultureName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(cultureName))
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            while (!culture.Equals(CultureInfo.InvariantCulture))
            {
                if (seen.Add(culture.Name))
                {
                    yield return culture.Name;
                }

                culture = culture.Parent;
            }
        }

        if (seen.Add(DefaultFallbackCulture))
        {
            yield return DefaultFallbackCulture;
        }
    }

    private sealed record CultureCacheEntry(DateTime LastWriteUtc, IReadOnlyDictionary<string, string> Values);
}
