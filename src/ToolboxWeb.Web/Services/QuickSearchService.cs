using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ToolboxWeb.Web.Constants;
using ToolboxWeb.Web.Data;
using ToolboxWeb.Web.Enums;
using ToolboxWeb.Web.Extensions;

namespace ToolboxWeb.Web.Services;

/// <summary>
/// "Search my data" in the command palette (Ctrl+K): the user's notes, to-dos, quick links and
/// prompts that match a query, a few of each. Matching ignores case and Vietnamese accents
/// (<see cref="NoteSearch"/>), like the search boxes of those pages.
/// </summary>
public interface IQuickSearchService
{
    Task<IReadOnlyList<QuickSearchHit>> SearchAsync(string userId, string query, IUrlHelper url, int perKind = 5);
}

/// <param name="Kind">note, todo, link or prompt.</param>
/// <param name="External">True when Url leaves the app (a quick link), so it opens in a new tab.</param>
public sealed record QuickSearchHit(string Kind, int Id, string Title, string? Detail, string Url, bool External = false);

public sealed class QuickSearchService : IQuickSearchService
{
    /// <summary>Shorter queries match too much to be useful.</summary>
    public const int MinQueryLength = 2;

    private readonly ApplicationDbContext _db;
    private readonly INoteService _notes;
    private readonly IChecklistService _checklist;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public QuickSearchService(ApplicationDbContext db, INoteService notes, IChecklistService checklist, IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _notes = notes;
        _checklist = checklist;
        _localizer = localizer;
    }

    public async Task<IReadOnlyList<QuickSearchHit>> SearchAsync(string userId, string query, IUrlHelper url, int perKind = 5)
    {
        var terms = NoteSearch.Terms(query);
        if (query.Trim().Length < MinQueryLength || terms.Count == 0)
        {
            return [];
        }

        var hits = new List<QuickSearchHit>();
        var notesHref = url.RouteUrl(ToolboxRouteSlugs.RouteNames.NotesHtml) ?? "/notes.html";
        foreach (var note in await _notes.GetAsync(userId, query, perKind))
        {
            hits.Add(new QuickSearchHit("note", note.Id, note.Title, _localizer[$"Notes.Format.{note.Format}"].Value, $"{notesHref}?id={note.Id}"));
        }

        // To-dos go through ChecklistService, which skips deleted and archived rows.
        var checklistHref = url.RouteUrl(ToolboxRouteSlugs.RouteNames.ChecklistHtml) ?? "/checklist.html";
        var todos = (await _checklist.GetAsync(userId, 500))
            .Where(x => NoteSearch.Matches(terms, x.Title, x.Description))
            .Take(perKind);
        foreach (var todo in todos)
        {
            var detail = todo.Status == ChecklistStatus.Done
                ? _localizer["Checklist.Filter.Done"].Value
                : todo.DueDate is { } due ? _localizer["Search.DueOn", due.ToDisplayDate()].Value : null;
            hits.Add(new QuickSearchHit("todo", todo.Id, todo.Title, detail,
                $"{checklistHref}?search={Uri.EscapeDataString(todo.Title)}&focus={todo.Id}"));
        }

        var links = await _db.QuickLinks.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        foreach (var link in links.Where(x => NoteSearch.Matches(terms, x.Title, x.Url, x.GroupName))
                     .OrderByDescending(x => x.OpenCount).Take(perKind))
        {
            hits.Add(new QuickSearchHit("link", link.Id, link.Title, link.Url, link.Url, External: true));
        }

        var promptsHref = url.RouteUrl(ToolboxRouteSlugs.RouteNames.PromptTemplatesHtml) ?? "/prompt-templates.html";
        var prompts = await _db.PromptTemplates.AsNoTracking().Where(x => x.UserId == userId).ToListAsync();
        foreach (var prompt in prompts.Where(x => NoteSearch.Matches(terms, x.Name, x.Description, x.Content))
                     .OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.UseCount).Take(perKind))
        {
            hits.Add(new QuickSearchHit("prompt", prompt.Id, prompt.Name, _localizer[$"PromptCategory.{prompt.Category}"].Value,
                $"{promptsHref}?search={Uri.EscapeDataString(prompt.Name)}&open={prompt.Id}"));
        }

        return hits;
    }
}
