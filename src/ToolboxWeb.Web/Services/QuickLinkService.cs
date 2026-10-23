using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Bookmarks;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

public interface IQuickLinkService
{
    Task<QuickLinksIndexViewModel> GetListAsync(string userId, QuickLinkListQuery query);

    /// <summary>The links a person opens first: pinned, then the most opened.</summary>
    Task<IReadOnlyList<QuickLink>> GetTopAsync(string userId, int take);

    Task<QuickLink?> GetByIdAsync(string userId, int id);
    Task<int> CreateAsync(string userId, QuickLinkFormViewModel model);
    Task<bool> UpdateAsync(string userId, QuickLinkFormViewModel model);
    Task<QuickLink?> MarkOpenedAsync(string userId, int id);
    Task<bool?> TogglePinAsync(string userId, int id);

    /// <summary>Gives the links the order of <paramref name="ids"/>; ids that are not the user's are ignored.</summary>
    Task ReorderAsync(string userId, IReadOnlyList<int> ids);

    /// <summary>The title of another link of the user with the same address, if any.</summary>
    Task<string?> FindDuplicateAsync(string userId, string url, int? exceptId);

    Task<bool> DeleteAsync(string userId, int id);
    Task<QuickLinkImportResult> ImportAsync(string userId, IReadOnlyList<BookmarkEntry> entries);
    Task<IReadOnlyList<BookmarkEntry>> ExportAsync(string userId);
}

public class QuickLinkService : IQuickLinkService
{
    /// <summary>A browser export can hold thousands of bookmarks; quick links are a short list.</summary>
    public const int MaxImportItems = 1000;

    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public QuickLinkService(ApplicationDbContext db, IActivityLogService activityLog, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _localizer = localizer;
    }

    public async Task<QuickLinksIndexViewModel> GetListAsync(string userId, QuickLinkListQuery query)
    {
        // A person's links number in the dozens, so one read and in-memory shaping lets the
        // search ignore case and Vietnamese accents (NoteSearch) on title, address and group.
        var all = await _db.QuickLinks.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        var terms = NoteSearch.Terms(query.Search);
        var matching = all.Where(x => NoteSearch.Matches(terms, x.Title, x.Url, x.GroupName)).ToList();

        var groups = matching
            .GroupBy(x => GroupKey(x.GroupName))
            .Select(x => new QuickLinkGroupCount(x.Key, x.First().GroupName?.Trim() is { Length: > 0 } name ? name : null, x.Count()))
            .OrderBy(x => x.GroupName is null)
            .ThenBy(x => x.GroupName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var filtered = query.Group is { Length: > 0 } group
            ? matching.Where(x => GroupKey(x.GroupName) == group).ToList()
            : matching;

        var sections = new List<QuickLinkSection>();
        var pinned = Order(filtered.Where(x => x.IsPinned), query.Sort).ToList();
        if (pinned.Count > 0)
        {
            sections.Add(new QuickLinkSection("_pinned", null, true, pinned));
        }

        foreach (var entry in groups)
        {
            var links = Order(filtered.Where(x => !x.IsPinned && GroupKey(x.GroupName) == entry.Key), query.Sort).ToList();
            if (links.Count > 0)
            {
                sections.Add(new QuickLinkSection(entry.Key, entry.GroupName, false, links));
            }
        }

        return new QuickLinksIndexViewModel
        {
            Query = query,
            Sections = sections,
            Total = all.Count,
            PinnedCount = all.Count(x => x.IsPinned),
            Groups = groups,
            SearchCount = matching.Count,
            GroupNames = all.Select(x => x.GroupName?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Select(x => x!)
                .Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
    }

    public async Task<IReadOnlyList<QuickLink>> GetTopAsync(string userId, int take)
    {
        return await _db.QuickLinks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.OpenCount)
            .ThenBy(x => x.GroupName)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .Take(take)
            .ToListAsync();
    }

    public async Task<QuickLink?> GetByIdAsync(string userId, int id)
    {
        return await _db.QuickLinks.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
    }

    public async Task<int> CreateAsync(string userId, QuickLinkFormViewModel model)
    {
        var group = await ResolveGroupAsync(userId, model.GroupName);
        var link = new QuickLink
        {
            UserId = userId,
            Title = model.Title.Trim(),
            Url = QuickLinkUrl.Normalize(model.Url) ?? model.Url.Trim(),
            GroupName = group,
            SortOrder = await NextSortOrderAsync(userId, group),
            CreatedAt = DateTime.UtcNow
        };

        _db.QuickLinks.Add(link);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkCreatedSummary", link.Title].Value);
        return link.Id;
    }

    public async Task<bool> UpdateAsync(string userId, QuickLinkFormViewModel model)
    {
        var link = model.Id is int id ? await _db.QuickLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id) : null;
        if (link is null)
        {
            return false;
        }

        var group = await ResolveGroupAsync(userId, model.GroupName);
        if (!string.Equals(GroupKey(link.GroupName), GroupKey(group), StringComparison.Ordinal))
        {
            // Moved to another group: it goes to the end of that group.
            link.SortOrder = await NextSortOrderAsync(userId, group);
        }

        link.Title = model.Title.Trim();
        link.Url = QuickLinkUrl.Normalize(model.Url) ?? model.Url.Trim();
        link.GroupName = group;
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkUpdatedSummary", link.Title].Value);
        return true;
    }

    public async Task<QuickLink?> MarkOpenedAsync(string userId, int id)
    {
        var link = await _db.QuickLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (link is null)
        {
            return null;
        }

        link.OpenCount++;
        link.LastOpenedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Use, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkOpenedSummary", link.Title].Value);
        return link;
    }

    public async Task<bool?> TogglePinAsync(string userId, int id)
    {
        var link = await _db.QuickLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (link is null)
        {
            return null;
        }

        link.IsPinned = !link.IsPinned;
        await _db.SaveChangesAsync();
        return link.IsPinned;
    }

    public async Task ReorderAsync(string userId, IReadOnlyList<int> ids)
    {
        var links = await _db.QuickLinks.Where(x => x.UserId == userId && ids.Contains(x.Id)).ToListAsync();
        var byId = links.ToDictionary(x => x.Id);
        var position = 0;
        foreach (var id in ids.Distinct())
        {
            if (byId.TryGetValue(id, out var link))
            {
                link.SortOrder = position++;
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> FindDuplicateAsync(string userId, string url, int? exceptId)
    {
        var key = QuickLinkUrl.Key(QuickLinkUrl.Normalize(url) ?? url);
        if (key.Length == 0)
        {
            return null;
        }

        var links = await _db.QuickLinks.AsNoTracking()
            .Where(x => x.UserId == userId && x.Id != (exceptId ?? 0))
            .Select(x => new { x.Title, x.Url })
            .ToListAsync();
        return links.FirstOrDefault(x => QuickLinkUrl.Key(x.Url) == key)?.Title;
    }

    public async Task<bool> DeleteAsync(string userId, int id)
    {
        var link = await _db.QuickLinks.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (link is null)
        {
            return false;
        }

        _db.QuickLinks.Remove(link);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.QuickLink, link.Id.ToString(), _localizer["ActivityLog.QuickLinkDeletedSummary", link.Title].Value);
        return true;
    }

    /// <summary>
    /// Adds the bookmarks of a browser export. Addresses the user already has, addresses that
    /// are not web or file links (javascript:, place:, …) and entries over the field limits are
    /// skipped; folders become groups.
    /// </summary>
    public async Task<QuickLinkImportResult> ImportAsync(string userId, IReadOnlyList<BookmarkEntry> entries)
    {
        var existing = await _db.QuickLinks.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Url).ToListAsync();
        var seen = new HashSet<string>(existing.Select(QuickLinkUrl.Key), StringComparer.Ordinal);
        var nextOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        var groupNames = await GroupNamesAsync(userId);

        var created = 0;
        var skipped = Math.Max(0, entries.Count - MaxImportItems);
        foreach (var entry in entries.Take(MaxImportItems))
        {
            var url = QuickLinkUrl.Normalize(entry.Url);
            var title = entry.Title.Trim();
            if (url is null || url.Length > 1000 || title.Length == 0 || !seen.Add(QuickLinkUrl.Key(url)))
            {
                skipped++;
                continue;
            }

            // A folder named like an existing group (any case) joins that group.
            var group = Group(entry.Folder is { Length: > 80 } folder ? folder[..80] : entry.Folder);
            var key = GroupKey(group);
            if (group is not null)
            {
                group = groupNames.TryGetValue(key, out var existingName) ? existingName : groupNames[key] = group;
            }

            if (!nextOrder.TryGetValue(key, out var order))
            {
                order = await NextSortOrderAsync(userId, group);
            }

            nextOrder[key] = order + 1;
            _db.QuickLinks.Add(new QuickLink
            {
                UserId = userId,
                Title = title.Length > 120 ? title[..120] : title,
                Url = url,
                GroupName = group,
                SortOrder = order,
                CreatedAt = DateTime.UtcNow
            });
            created++;
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync();
            await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.QuickLink, null, _localizer["ActivityLog.QuickLinksImportedSummary", created].Value);
        }

        return new QuickLinkImportResult(created, skipped);
    }

    public async Task<IReadOnlyList<BookmarkEntry>> ExportAsync(string userId)
    {
        var links = await _db.QuickLinks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.GroupName)
            .ThenBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync();
        return links.Select(x => new BookmarkEntry(x.Title, x.Url, x.GroupName)).ToList();
    }

    private static IEnumerable<QuickLink> Order(IEnumerable<QuickLink> links, string sort) => sort switch
    {
        QuickLinkSorts.Name => links.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase),
        QuickLinkSorts.MostOpened => links.OrderByDescending(x => x.OpenCount).ThenByDescending(x => x.LastOpenedAt).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase),
        _ => links.OrderBy(x => x.SortOrder).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase)
    };

    // Groups match ignoring case (GroupKey), as on the page, so "Dev" and "dev" share one order.
    // SQLite's lower() only folds ASCII, so the comparison runs here, not in the query.
    private async Task<int> NextSortOrderAsync(string userId, string? group)
    {
        var key = GroupKey(group);
        var links = await _db.QuickLinks.AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.GroupName, x.SortOrder })
            .ToListAsync();
        return links.Where(x => GroupKey(x.GroupName) == key).Select(x => (int?)x.SortOrder).Max() is int max ? max + 1 : 0;
    }

    /// <summary>The group name to store: the spelling the user already has when only the case differs.</summary>
    private async Task<string?> ResolveGroupAsync(string userId, string? value)
    {
        var group = Group(value);
        return group is not null && (await GroupNamesAsync(userId)).TryGetValue(GroupKey(group), out var existing)
            ? existing
            : group;
    }

    private async Task<Dictionary<string, string>> GroupNamesAsync(string userId)
    {
        var names = await _db.QuickLinks.AsNoTracking()
            .Where(x => x.UserId == userId && x.GroupName != null)
            .OrderBy(x => x.Id)
            .Select(x => x.GroupName!)
            .ToListAsync();
        var byKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names.Select(x => x.Trim()).Where(x => x.Length > 0))
        {
            byKey.TryAdd(GroupKey(name), name);
        }

        return byKey;
    }

    private static string? Group(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Filter key of a group: its name ignoring case, or <see cref="QuickLinkSorts.NoGroup"/>.</summary>
    public static string GroupKey(string? group)
    {
        return string.IsNullOrWhiteSpace(group) ? QuickLinkSorts.NoGroup : group.Trim().ToLowerInvariant();
    }
}

/// <summary>Quick link addresses: what counts as valid, how one typed without a scheme is completed, and when two are the same.</summary>
public static class QuickLinkUrl
{
    private static readonly string[] Schemes = [Uri.UriSchemeHttp, Uri.UriSchemeHttps, Uri.UriSchemeFtp, Uri.UriSchemeFile];

    /// <summary>The address to store, or null when it is not a web, ftp or file address.</summary>
    public static string? Normalize(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return null;
        }

        // "jira.company.vn/browse" → "https://jira.company.vn/browse"
        if (!text.Contains("://", StringComparison.Ordinal) && !text.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            text = "https://" + text;
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && Schemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)
            && (uri.IsFile || uri.Host.Length > 0)
            ? text
            : null;
    }

    /// <summary>Compares addresses without case in the host, the scheme, "www." and a trailing slash.</summary>
    public static string Key(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri))
        {
            return (url ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return host + (uri.IsDefaultPort ? string.Empty : ":" + uri.Port) + uri.PathAndQuery.TrimEnd('/') + uri.Fragment;
    }
}
