using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Notes;
using ToolboxWeb.Web.ViewModels;

namespace ToolboxWeb.Web.Services;

/// <summary>What the notes page is asked to show. Everything travels on the query string.</summary>
public sealed record NotePageQuery(string? FormatSlug, string? Search, int? SelectedId, int? FocusChildId = null);

/// <summary>Outcome of a create or update. <see cref="GroupId"/> is the top-level note to show next.</summary>
public sealed record NoteSaveResult(bool Succeeded, int? GroupId = null, int? NoteId = null, string? Error = null)
{
    public static NoteSaveResult Fail(string error) => new(false, Error: error);
}

/// <summary><see cref="GroupIdToShow"/> is the group a deleted sub-note belonged to, or null for a deleted group.</summary>
public sealed record NoteDeleteResult(int? GroupIdToShow, int DeletedChildren);

public enum NoteSecretStatus
{
    Found,
    NoPassword,
    NotFound,
    DecryptFailed
}

public sealed record NoteSecretResult(NoteSecretStatus Status, string? Password = null);

public interface INoteService
{
    /// <summary>Top-level notes only, pinned first then newest; used by the dashboard.</summary>
    Task<IReadOnlyList<Note>> GetAsync(string userId, string? search = null, int take = 100);

    Task<Note?> GetByIdAsync(string userId, int id);
    Task<NotesIndexViewModel> GetPageAsync(string userId, NotePageQuery query);
    Task<NoteSaveResult> CreateAsync(string userId, NoteFormViewModel model);
    Task<NoteSaveResult> UpdateAsync(string userId, NoteFormViewModel model);
    Task<NoteDeleteResult?> DeleteAsync(string userId, int id);

    /// <summary>Returns the new pinned state, or null when the note is not the user's top-level note.</summary>
    Task<bool?> TogglePinAsync(string userId, int id);

    Task<NoteSecretResult> RevealServerPasswordAsync(string userId, int id);
}

public class NoteService : INoteService
{
    private const int PreviewLength = 140;
    private const int MaxLinks = 20;

    // Only http(s) is ever rendered as a link, so a note cannot smuggle in javascript: URLs.
    private static readonly Regex LinkPattern = new(@"https?://[^\s<>""'`]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly ApplicationDbContext _db;
    private readonly IActivityLogService _activityLog;
    private readonly INoteSecretProtector _secrets;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public NoteService(
        ApplicationDbContext db,
        IActivityLogService activityLog,
        INoteSecretProtector secrets,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _activityLog = activityLog;
        _secrets = secrets;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<Note>> GetAsync(string userId, string? search = null, int take = 100)
    {
        var query = _db.Notes.AsNoTracking()
            .Where(x => x.UserId == userId && x.ParentId == null)
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAt ?? x.CreatedAt);

        var terms = NoteSearch.Terms(search);
        if (terms.Count == 0)
        {
            return await query.Take(take).ToListAsync();
        }

        var notes = await query.ToListAsync();
        return notes.Where(x => NoteSearch.Matches(terms, SearchFields(x))).Take(take).ToList();
    }

    public Task<Note?> GetByIdAsync(string userId, int id)
    {
        return _db.Notes.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
    }

    public async Task<NotesIndexViewModel> GetPageAsync(string userId, NotePageQuery query)
    {
        // A person's notes number in the hundreds at most, so one read and in-memory shaping
        // beats several round trips and lets search ignore case and Vietnamese accents.
        var all = await _db.Notes.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();

        var childrenByGroup = all
            .Where(x => x.ParentId is not null)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToList());

        var groups = all.Where(x => x.ParentId is null).ToList();
        var terms = NoteSearch.Terms(query.Search);
        var activeFormat = NoteFormatCatalog.FindBySlug(query.FormatSlug);

        // Search first, ignoring the format filter, so each chip can say how many hits it holds.
        var hits = new List<(Note Group, bool InChild)>();
        foreach (var group in groups)
        {
            if (NoteSearch.Matches(terms, SearchFields(group)))
            {
                hits.Add((group, false));
                continue;
            }

            if (childrenByGroup.TryGetValue(group.Id, out var children)
                && children.Any(child => NoteSearch.Matches(terms, SearchFields(child))))
            {
                hits.Add((group, true));
            }
        }

        var formatCounts = NoteFormatCatalog.All
            .Select(format => new NoteFormatCount
            {
                Format = format,
                Count = hits.Count(hit => hit.Group.Format == format.Format)
            })
            .ToArray();

        var visible = hits
            .Where(hit => activeFormat is null || hit.Group.Format == activeFormat.Format)
            .OrderByDescending(hit => hit.Group.IsPinned)
            .ThenByDescending(hit => Touched(hit.Group))
            .ThenByDescending(hit => hit.Group.Id)
            .ToList();

        // A sub-note id selects its group and opens that sub-note.
        Note? selected = null;
        var focusChildId = query.FocusChildId;
        if (query.SelectedId is int selectedId)
        {
            selected = all.FirstOrDefault(x => x.Id == selectedId);
            if (selected?.ParentId is int parentId)
            {
                focusChildId ??= selected.Id;
                selected = all.FirstOrDefault(x => x.Id == parentId);
            }
        }

        var hasExplicitSelection = selected is not null;
        selected ??= visible.Select(hit => hit.Group).FirstOrDefault();

        return new NotesIndexViewModel
        {
            Search = query.Search?.Trim(),
            ActiveFormat = activeFormat,
            TotalCount = all.Count,
            FormatsInUse = groups.Select(x => x.Format).Distinct().Count(),
            MatchingCount = hits.Count,
            FormatCounts = formatCounts,
            Items = visible.Select(hit => ToListItem(
                    hit.Group,
                    childrenByGroup.TryGetValue(hit.Group.Id, out var children) ? children : [],
                    hit.InChild,
                    hit.Group.Id == selected?.Id))
                .ToArray(),
            Selected = selected is null
                ? null
                : ToDetail(selected, childrenByGroup.TryGetValue(selected.Id, out var selectedChildren) ? selectedChildren : []),
            HasExplicitSelection = hasExplicitSelection,
            FocusChildId = focusChildId,
            Form = new NoteFormViewModel { Format = activeFormat?.Format ?? NoteFormat.Text }
        };
    }

    public async Task<NoteSaveResult> CreateAsync(string userId, NoteFormViewModel model)
    {
        Note? parent = null;
        if (model.ParentId is int parentId)
        {
            parent = await _db.Notes.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == parentId);

            // One level only: a sub-note cannot become a group of its own.
            if (parent is null || parent.ParentId is not null)
            {
                return NoteSaveResult.Fail(_localizer["Notes.InvalidParent"].Value);
            }
        }

        var format = EffectiveFormat(model.Format, parent);

        var note = new Note
        {
            UserId = userId,
            ParentId = parent?.Id,
            Title = model.Title.Trim(),

            // A connection is a title plus its details; it has no description of its own.
            Content = IsConnection(format, parent?.Id) ? string.Empty : ContentOf(format, model),

            // Tags and pinning organise the list, which only shows groups.
            Tags = parent is null ? CleanOptional(model.Tags) : null,
            IsPinned = parent is null && model.IsPinned,
            Format = format,
            CreatedAt = DateTime.UtcNow
        };

        ApplyServer(userId, note, model);

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Create, ActivityEntityType.Note, note.Id.ToString(),
            _localizer["ActivityLog.NoteCreatedSummary", note.Title].Value);

        return new NoteSaveResult(true, parent?.Id ?? note.Id, note.Id);
    }

    public async Task<NoteSaveResult> UpdateAsync(string userId, NoteFormViewModel model)
    {
        if (model.Id is null)
        {
            return NoteSaveResult.Fail(_localizer["Notes.NotFound"].Value);
        }

        var note = await _db.Notes
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == model.Id.Value);
        if (note is null)
        {
            return NoteSaveResult.Fail(_localizer["Notes.NotFound"].Value);
        }

        var parent = note.ParentId is int parentId
            ? await _db.Notes.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == parentId)
            : null;

        note.Title = model.Title.Trim();
        note.Content = ContentOf(EffectiveFormat(model.Format, parent), model);

        // Pinning has its own action; the edit form does not carry it, so it is left alone here.
        if (note.ParentId is null)
        {
            note.Tags = CleanOptional(model.Tags);
        }

        var format = EffectiveFormat(model.Format, parent);
        if (note.ParentId is null && format != note.Format && format != NoteFormat.Mixed)
        {
            // Sub-notes of an ordinary group share its format. Becoming Mixed leaves each
            // sub-note with the format it already had.
            foreach (var child in note.Children)
            {
                child.Format = format;
            }
        }

        note.Format = format;
        ApplyServer(userId, note, model);
        note.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _activityLog.LogAsync(userId, ActivityActionType.Update, ActivityEntityType.Note, note.Id.ToString(),
            _localizer["ActivityLog.NoteUpdatedSummary", note.Title].Value);

        return new NoteSaveResult(true, note.ParentId ?? note.Id, note.Id);
    }

    public async Task<NoteDeleteResult?> DeleteAsync(string userId, int id)
    {
        var note = await _db.Notes
            .Include(x => x.Children)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (note is null)
        {
            return null;
        }

        var childCount = note.Children.Count;
        var parentId = note.ParentId;
        var title = note.Title;

        // The loaded children go with it; the FK cascades as well for anything not loaded.
        _db.Notes.Remove(note);
        await _db.SaveChangesAsync();

        var summary = childCount > 0
            ? _localizer["ActivityLog.NoteDeletedWithChildrenSummary", title, childCount].Value
            : _localizer["ActivityLog.NoteDeletedSummary", title].Value;
        await _activityLog.LogAsync(userId, ActivityActionType.Delete, ActivityEntityType.Note, id.ToString(), summary);

        return new NoteDeleteResult(parentId, childCount);
    }

    public async Task<bool?> TogglePinAsync(string userId, int id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id && x.ParentId == null);
        if (note is null)
        {
            return null;
        }

        note.IsPinned = !note.IsPinned;
        await _db.SaveChangesAsync();
        return note.IsPinned;
    }

    public async Task<NoteSecretResult> RevealServerPasswordAsync(string userId, int id)
    {
        var note = await _db.Notes.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id);
        if (note is null)
        {
            return new NoteSecretResult(NoteSecretStatus.NotFound);
        }

        if (string.IsNullOrEmpty(note.Server.ProtectedPassword))
        {
            return new NoteSecretResult(NoteSecretStatus.NoPassword);
        }

        var password = _secrets.Unprotect(userId, note.Server.ProtectedPassword);
        if (password is null)
        {
            return new NoteSecretResult(NoteSecretStatus.DecryptFailed);
        }

        // Reading a stored credential is worth a trail.
        await _activityLog.LogAsync(userId, ActivityActionType.Use, ActivityEntityType.Note, note.Id.ToString(),
            _localizer["ActivityLog.NoteSecretRevealedSummary", note.Title].Value);

        return new NoteSecretResult(NoteSecretStatus.Found, password);
    }

    /// <summary>
    /// Sub-notes of an ordinary group take the group's format. In a Mixed group each sub-note
    /// picks its own, except Mixed itself: a group cannot nest another group.
    /// </summary>
    private static NoteFormat EffectiveFormat(NoteFormat requested, Note? parent)
    {
        if (!Enum.IsDefined(requested))
        {
            requested = NoteFormat.Text;
        }

        if (parent is null)
        {
            return requested;
        }

        if (parent.Format != NoteFormat.Mixed)
        {
            return parent.Format;
        }

        return requested == NoteFormat.Mixed ? NoteFormat.Text : requested;
    }

    /// <summary>
    /// Link and checklist notes are edited as rows, and the rows become the note's text. A form
    /// with no rows filled in (a plain textarea, or a Mixed sub-note on another format) keeps
    /// posting the text itself.
    /// </summary>
    private static string ContentOf(NoteFormat format, NoteFormViewModel model) => format switch
    {
        NoteFormat.Link when NoteRows.HasAny(model.LinkUrls) => NoteRows.FromLinks(model.LinkNames, model.LinkUrls),
        NoteFormat.Checklist when NoteRows.HasAny(model.Items) => NoteRows.FromTasks(model.Items, model.ItemStatuses),

        // Rows that were all cleared empty the note, but only when that editor was the one on screen.
        NoteFormat.Link when model.LinkUrls is not null && model.Content is null => string.Empty,
        NoteFormat.Checklist when model.Items is not null && model.Content is null => string.Empty,
        _ => CleanContent(model.Content)
    };

    /// <summary>
    /// Only a sub-note of the Server format is a connection. A server group is a title and a
    /// description, and its sub-notes are the connections.
    /// </summary>
    private static bool IsConnection(NoteFormat format, int? parentId) =>
        format == NoteFormat.Server && parentId is not null;

    /// <summary>
    /// Writes the details of a connection. Anything else leaves stored details untouched, so
    /// switching format by mistake and back does not lose them.
    /// </summary>
    private void ApplyServer(string userId, Note note, NoteFormViewModel model)
    {
        if (!IsConnection(note.Format, note.ParentId))
        {
            return;
        }

        note.Server.Host = CleanOptional(model.Host);
        note.Server.Port = model.Port;
        note.Server.Username = CleanOptional(model.Username);
        note.Server.Database = CleanOptional(model.Database);

        if (model.ClearPassword)
        {
            note.Server.ProtectedPassword = null;
        }
        else if (!string.IsNullOrEmpty(model.NewPassword))
        {
            // Not trimmed: leading or trailing spaces can be part of a real password.
            note.Server.ProtectedPassword = _secrets.Protect(userId, model.NewPassword);
        }
    }

    private static string?[] SearchFields(Note note) =>
    [
        note.Title,
        note.Content,
        note.Tags,
        note.Server.Host,
        note.Server.Username,
        note.Server.Database
    ];

    private NoteListItemViewModel ToListItem(Note note, IReadOnlyList<Note> children, bool matchedInChild, bool isSelected) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Format = NoteFormatCatalog.Get(note.Format),
        IsPinned = note.IsPinned,
        ChildCount = children.Count,
        Preview = Preview(note, children),
        UpdatedUtc = AsUtc(Touched(note)),
        MatchedInChild = matchedInChild,
        IsSelected = isSelected
    };

    private static NoteDetailViewModel ToDetail(Note note, IReadOnlyList<Note> children) => new()
    {
        Id = note.Id,
        Title = note.Title,
        Content = note.Content,
        Format = NoteFormatCatalog.Get(note.Format),
        UpdatedUtc = AsUtc(Touched(note)),
        Server = ToServer(note.Server),
        Links = LinksOf(note),
        Tags = note.Tags,
        IsPinned = note.IsPinned,
        Children = children.Select(child => new NoteChildViewModel
        {
            Id = child.Id,
            Title = child.Title,
            Content = child.Content,
            Format = NoteFormatCatalog.Get(child.Format),
            UpdatedUtc = AsUtc(Touched(child)),
            Server = ToServer(child.Server),
            Links = LinksOf(child),
            LinkRows = child.Format == NoteFormat.Link ? NoteRows.ParseLinks(child.Content) : [],
            TaskRows = child.Format == NoteFormat.Checklist ? NoteRows.ParseTasks(child.Content) : []
        }).ToArray()
    };

    private static NoteServerViewModel ToServer(NoteServerConnection server) => new()
    {
        Host = server.Host,
        Port = server.Port,
        Username = server.Username,
        Database = server.Database,
        HasPassword = !string.IsNullOrEmpty(server.ProtectedPassword)
    };

    /// <summary>
    /// The description's first words. A group without one says what its sub-notes hold instead,
    /// which is what the user scans the list for.
    /// </summary>
    private string Preview(Note note, IReadOnlyList<Note> children)
    {
        var flat = Whitespace.Replace(note.Content, " ").Trim();
        if (flat.Length == 0)
        {
            flat = ChildrenSummary(note, children);
        }

        return flat.Length > PreviewLength ? flat[..PreviewLength].TrimEnd() + "…" : flat;
    }

    private string ChildrenSummary(Note note, IReadOnlyList<Note> children) => note.Format switch
    {
        NoteFormat.Server => string.Join(" · ", children
            .Where(child => child.Format == NoteFormat.Server)
            .Select(child => ToServer(child.Server).Display)
            .Where(display => display.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)),

        NoteFormat.Link => LinkTotal(children) is var links and > 0
            ? _localizer["Notes.Link.Count", links].Value
            : string.Empty,

        NoteFormat.Checklist => TaskRows(children) is { Count: > 0 } rows
            ? _localizer["Notes.Task.GroupPreview", rows.Count, NoteRows.DoneCount(rows)].Value
            : string.Empty,

        _ => string.Empty
    };

    private static int LinkTotal(IReadOnlyList<Note> children) =>
        children.Where(child => child.Format == NoteFormat.Link).Sum(child => NoteRows.ParseLinks(child.Content).Count);

    private static IReadOnlyList<NoteTaskRow> TaskRows(IReadOnlyList<Note> children) =>
        children.Where(child => child.Format == NoteFormat.Checklist)
            .SelectMany(child => NoteRows.ParseTasks(child.Content))
            .ToArray();

    private static IReadOnlyList<string> LinksOf(Note note)
    {
        if (note.Format != NoteFormat.Link || string.IsNullOrEmpty(note.Content))
        {
            return [];
        }

        return LinkPattern.Matches(note.Content)
            .Select(match => match.Value.TrimEnd('.', ',', ';', ':', ')', ']', '}', '!', '?'))
            .Where(url => Uri.TryCreate(url, UriKind.Absolute, out var uri)
                          && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxLinks)
            .ToArray();
    }

    private static DateTime Touched(Note note) => note.UpdatedAt ?? note.CreatedAt;

    // SQLite hands dates back as Unspecified; they were written as UTC.
    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>
    /// Drops blank lines around the content but keeps the first line's indentation, which is
    /// meaningful in code. The old service trimmed both ends and lost it.
    /// </summary>
    private static string CleanContent(string? content) =>
        (content ?? string.Empty).TrimEnd().TrimStart('\r', '\n');

    private static string? CleanOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
