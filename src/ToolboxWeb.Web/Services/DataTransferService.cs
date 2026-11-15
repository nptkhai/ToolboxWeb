using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Domain;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Infrastructure.Hosting;
using ToolboxWeb.Web.Infrastructure.Storage;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// "Toolkit data" file (format <c>toolkit.export</c>, version 1): everything a person keeps in
/// Toolkit, to move it from the shared IIS site to a desktop (or between desktops). Notes with
/// their sub-notes, to-dos (not the deleted ones), focus sessions, quick links, prompts and the
/// profile photo. Not included: the activity log, the developer demo rows, and saved server
/// passwords (they are encrypted with the keys of the copy that saved them).
/// </summary>
public sealed class DataTransferFile
{
    public const string FormatName = "toolkit.export";

    public string Format { get; set; } = FormatName;
    public int Version { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; }
    public string? AppVersion { get; set; }
    public List<ExportNote> Notes { get; set; } = [];
    public List<ExportChecklistItem> Checklist { get; set; } = [];
    public List<ExportFocusSession> FocusSessions { get; set; } = [];
    public List<ExportQuickLink> QuickLinks { get; set; } = [];
    public List<ExportPrompt> Prompts { get; set; } = [];
    public ExportAvatar? Avatar { get; set; }
}

public sealed record ExportNote(int Id, int? ParentId, string Title, string Content, string? Tags, bool IsPinned, NoteFormat Format,
    DateTime CreatedAt, DateTime? UpdatedAt, string? Host, int? Port, string? Username, string? Database);

public sealed record ExportChecklistItem(int Id, string Title, string? Description, ChecklistStatus Status, ChecklistPriority Priority,
    DateTime? DueDate, DateTime CreatedAt, DateTime? CompletedAt);

public sealed record ExportFocusSession(int DurationMinutes, DateTime StartedAt, DateTime CompletedAt, string? Note, int? ChecklistItemId);

public sealed record ExportQuickLink(string Title, string Url, string? GroupName, int SortOrder, bool IsPinned, int OpenCount,
    DateTime CreatedAt, DateTime? LastOpenedAt);

public sealed record ExportPrompt(string Name, PromptCategory Category, string? Description, string? InputVariables, string? OutputFormat,
    string Content, bool IsPinned, int UseCount, DateTime CreatedAt, DateTime? UpdatedAt, DateTime? LastUsedAt);

public sealed record ExportAvatar(string Extension, string Base64);

/// <summary>How many items of each kind a file brings, and how many of them are already here.</summary>
public sealed record DataTransferCount(string Kind, int New, int Existing);

public sealed record DataTransferPreview(IReadOnlyList<DataTransferCount> Counts, bool HasAvatar, DateTime ExportedAtUtc, string? AppVersion)
{
    public int TotalNew => Counts.Sum(x => x.New);
}

public interface IDataTransferService
{
    Task<byte[]> ExportAsync(string userId);

    /// <summary>Reads a file; null with an error key when it is not a Toolkit data file.</summary>
    (DataTransferFile? File, string? ErrorKey) Read(Stream stream);

    /// <summary>What an import would add. Items already here (same title and time, same address, same prompt name, …) are skipped.</summary>
    Task<DataTransferPreview> PreviewAsync(string userId, DataTransferFile file);

    /// <summary>Adds the new items to the user's data in one transaction; returns what was added.</summary>
    Task<DataTransferPreview> ImportAsync(string userId, DataTransferFile file);
}

public sealed class DataTransferService(ApplicationDbContext db, RuntimeStoragePaths storage, ILogger<DataTransferService> logger) : IDataTransferService
{
    public const long MaxFileBytes = 50 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly HashSet<string> AvatarExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public async Task<byte[]> ExportAsync(string userId)
    {
        var notes = await db.Notes.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id).ToListAsync();
        var file = new DataTransferFile
        {
            ExportedAtUtc = DateTime.UtcNow,
            AppVersion = Infrastructure.Hosting.AppVersion.Version,
            Notes = notes.Select(x => new ExportNote(x.Id, x.ParentId, x.Title, x.Content, x.Tags, x.IsPinned, x.Format, x.CreatedAt, x.UpdatedAt,
                x.Server.Host, x.Server.Port, x.Server.Username, x.Server.Database)).ToList(),
            Checklist = await db.ChecklistItems.AsNoTracking().Where(x => x.UserId == userId && x.DeletedAt == null).OrderBy(x => x.Id)
                .Select(x => new ExportChecklistItem(x.Id, x.Title, x.Description, x.Status, x.Priority, x.DueDate, x.CreatedAt, x.CompletedAt)).ToListAsync(),
            FocusSessions = await db.FocusSessions.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.StartedAt)
                .Select(x => new ExportFocusSession(x.DurationMinutes, x.StartedAt, x.CompletedAt, x.Note, x.ChecklistItemId)).ToListAsync(),
            QuickLinks = await db.QuickLinks.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.SortOrder)
                .Select(x => new ExportQuickLink(x.Title, x.Url, x.GroupName, x.SortOrder, x.IsPinned, x.OpenCount, x.CreatedAt, x.LastOpenedAt)).ToListAsync(),
            Prompts = await db.PromptTemplates.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id)
                .Select(x => new ExportPrompt(x.Name, x.Category, x.Description, x.InputVariables, x.OutputFormat, x.Content, x.IsPinned, x.UseCount,
                    x.CreatedAt, x.UpdatedAt, x.LastUsedAt)).ToListAsync(),
            Avatar = await ReadAvatarAsync(userId)
        };
        return JsonSerializer.SerializeToUtf8Bytes(file, Json);
    }

    public (DataTransferFile? File, string? ErrorKey) Read(Stream stream)
    {
        try
        {
            var file = JsonSerializer.Deserialize<DataTransferFile>(stream, Json);
            if (file is null || file.Format != DataTransferFile.FormatName)
            {
                return (null, "DataTransfer.Error.NotToolkit");
            }

            return file.Version == 1 ? (file, null) : (null, "DataTransfer.Error.Version");
        }
        catch (JsonException)
        {
            return (null, "DataTransfer.Error.NotToolkit");
        }
    }

    public async Task<DataTransferPreview> PreviewAsync(string userId, DataTransferFile file) =>
        (await PlanAsync(userId, file)).Preview;

    public async Task<DataTransferPreview> ImportAsync(string userId, DataTransferFile file)
    {
        var plan = await PlanAsync(userId, file);
        await using var transaction = await db.Database.BeginTransactionAsync();

        // Notes: groups first, so sub-notes can point at their new (or already existing) group.
        var noteIds = new Dictionary<int, int>(plan.ExistingNotes);
        foreach (var note in plan.NewNotes.OrderBy(x => x.ParentId is null ? 0 : 1).ThenBy(x => x.Id))
        {
            int? parentId = note.ParentId is { } parent && noteIds.TryGetValue(parent, out var mapped) ? mapped : null;
            var entity = new Note
            {
                UserId = userId,
                ParentId = parentId,
                Title = note.Title,
                Content = note.Content,
                Tags = note.Tags,
                IsPinned = note.IsPinned,
                Format = note.Format,
                CreatedAt = note.CreatedAt,
                UpdatedAt = note.UpdatedAt,
                Server = new NoteServerConnection { Host = note.Host, Port = note.Port, Username = note.Username, Database = note.Database }
            };
            db.Notes.Add(entity);
            await db.SaveChangesAsync();
            noteIds[note.Id] = entity.Id;
        }

        var checklistIds = new Dictionary<int, int>(plan.ExistingChecklist);
        foreach (var item in plan.NewChecklist)
        {
            var entity = new ChecklistItem
            {
                UserId = userId,
                Title = item.Title,
                Description = item.Description,
                Status = item.Status,
                Priority = item.Priority,
                DueDate = item.DueDate,
                CreatedAt = item.CreatedAt,
                CompletedAt = item.CompletedAt
            };
            db.ChecklistItems.Add(entity);
            await db.SaveChangesAsync();
            checklistIds[item.Id] = entity.Id;
        }

        db.FocusSessions.AddRange(plan.NewFocus.Select(x => new FocusSession
        {
            UserId = userId,
            DurationMinutes = x.DurationMinutes,
            StartedAt = x.StartedAt,
            CompletedAt = x.CompletedAt,
            Note = x.Note,
            ChecklistItemId = x.ChecklistItemId is { } id && checklistIds.TryGetValue(id, out var mapped) ? mapped : null
        }));
        db.QuickLinks.AddRange(plan.NewLinks.Select(x => new QuickLink
        {
            UserId = userId,
            Title = x.Title,
            Url = x.Url,
            GroupName = x.GroupName,
            SortOrder = x.SortOrder,
            IsPinned = x.IsPinned,
            OpenCount = x.OpenCount,
            CreatedAt = x.CreatedAt,
            LastOpenedAt = x.LastOpenedAt
        }));
        db.PromptTemplates.AddRange(plan.NewPrompts.Select(x => new PromptTemplate
        {
            UserId = userId,
            Name = x.Name,
            Category = x.Category,
            Description = x.Description,
            InputVariables = x.InputVariables,
            OutputFormat = x.OutputFormat,
            Content = x.Content,
            IsPinned = x.IsPinned,
            UseCount = x.UseCount,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            LastUsedAt = x.LastUsedAt
        }));
        await db.SaveChangesAsync();

        if (plan.Preview.HasAvatar && file.Avatar is not null)
        {
            await SaveAvatarAsync(userId, file.Avatar);
        }

        await transaction.CommitAsync();
        return plan.Preview;
    }

    // ------------------------------------------------------------------ helpers

    private sealed record Plan(
        DataTransferPreview Preview,
        List<ExportNote> NewNotes,
        Dictionary<int, int> ExistingNotes,
        List<ExportChecklistItem> NewChecklist,
        Dictionary<int, int> ExistingChecklist,
        List<ExportFocusSession> NewFocus,
        List<ExportQuickLink> NewLinks,
        List<ExportPrompt> NewPrompts);

    private async Task<Plan> PlanAsync(string userId, DataTransferFile file)
    {
        // An item counts as already here when its identity matches: notes and to-dos by title and
        // creation time, focus sessions by start, links by address, prompts by name. Importing the
        // same file twice therefore adds nothing the second time.
        static string Stamp(DateTime value) => value.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);

        var notes = await db.Notes.AsNoTracking().Where(x => x.UserId == userId).Select(x => new { x.Id, x.Title, x.CreatedAt }).ToListAsync();
        var noteKeys = notes.GroupBy(x => x.Title + "|" + Stamp(x.CreatedAt)).ToDictionary(g => g.Key, g => g.First().Id);
        var existingNotes = new Dictionary<int, int>();
        var newNotes = new List<ExportNote>();
        foreach (var note in file.Notes)
        {
            if (noteKeys.TryGetValue(note.Title + "|" + Stamp(note.CreatedAt), out var id))
            {
                existingNotes[note.Id] = id;
            }
            else
            {
                newNotes.Add(note);
            }
        }

        var items = await db.ChecklistItems.AsNoTracking().Where(x => x.UserId == userId && x.DeletedAt == null).Select(x => new { x.Id, x.Title, x.CreatedAt }).ToListAsync();
        var itemKeys = items.GroupBy(x => x.Title + "|" + Stamp(x.CreatedAt)).ToDictionary(g => g.Key, g => g.First().Id);
        var existingChecklist = new Dictionary<int, int>();
        var newChecklist = new List<ExportChecklistItem>();
        foreach (var item in file.Checklist)
        {
            if (itemKeys.TryGetValue(item.Title + "|" + Stamp(item.CreatedAt), out var id))
            {
                existingChecklist[item.Id] = id;
            }
            else
            {
                newChecklist.Add(item);
            }
        }

        var starts = (await db.FocusSessions.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.StartedAt).ToListAsync()).Select(Stamp).ToHashSet();
        var newFocus = file.FocusSessions.Where(x => !starts.Contains(Stamp(x.StartedAt))).ToList();

        var urls = (await db.QuickLinks.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Url).ToListAsync()).Select(QuickLinkUrl.Key).ToHashSet();
        var newLinks = file.QuickLinks.Where(x => urls.Add(QuickLinkUrl.Key(x.Url))).ToList();

        var names = (await db.PromptTemplates.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.Name).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newPrompts = file.Prompts.Where(x => names.Add(x.Name)).ToList();

        var hasAvatar = file.Avatar is { } avatar && AvatarExtensions.Contains(avatar.Extension)
            && await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.AvatarUrl).FirstOrDefaultAsync() is null;

        var preview = new DataTransferPreview(
        [
            new("Notes", newNotes.Count, file.Notes.Count - newNotes.Count),
            new("Checklist", newChecklist.Count, file.Checklist.Count - newChecklist.Count),
            new("FocusSessions", newFocus.Count, file.FocusSessions.Count - newFocus.Count),
            new("QuickLinks", newLinks.Count, file.QuickLinks.Count - newLinks.Count),
            new("Prompts", newPrompts.Count, file.Prompts.Count - newPrompts.Count)
        ], hasAvatar, file.ExportedAtUtc, file.AppVersion);
        return new Plan(preview, newNotes, existingNotes, newChecklist, existingChecklist, newFocus, newLinks, newPrompts);
    }

    private async Task<ExportAvatar?> ReadAvatarAsync(string userId)
    {
        var url = await db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => x.AvatarUrl).FirstOrDefaultAsync();
        var prefix = $"/uploads/images/profile/{userId}/";
        if (url is null || !url.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var name = Path.GetFileName(url);
        var path = Path.Combine(storage.UploadsPath, "images", "profile", userId, name);
        return File.Exists(path) && AvatarExtensions.Contains(Path.GetExtension(name))
            ? new ExportAvatar(Path.GetExtension(name).ToLowerInvariant(), Convert.ToBase64String(await File.ReadAllBytesAsync(path)))
            : null;
    }

    private async Task SaveAvatarAsync(string userId, ExportAvatar avatar)
    {
        try
        {
            var bytes = Convert.FromBase64String(avatar.Base64);
            if (bytes.Length == 0 || bytes.Length > 5 * 1024 * 1024)
            {
                return;
            }

            var folder = Path.Combine(storage.UploadsPath, "images", "profile", userId);
            Directory.CreateDirectory(folder);
            var name = $"avatar-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{avatar.Extension.ToLowerInvariant()}";
            await File.WriteAllBytesAsync(Path.Combine(folder, name), bytes);
            var user = await db.Users.FirstAsync(x => x.Id == userId);
            user.AvatarUrl = $"/uploads/images/profile/{userId}/{name}";
            await db.SaveChangesAsync();
        }
        catch (FormatException ex)
        {
            logger.LogWarning(ex, "The profile photo in the imported file could not be read.");
        }
    }
}
