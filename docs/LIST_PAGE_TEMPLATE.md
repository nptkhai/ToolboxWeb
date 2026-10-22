# List page template

The structure every list screen follows. It comes from the Notes screen (the reference screen in `AGENTS.md`) and was first applied to the to-do list (`Views/Checklist/Index.cshtml`). Stage 4.5 of `docs/UI_UX_REVIEW.md`.

## Structure

```
<div class="tbx-list-page" data-list-page>
    _ListToolbar      totals · search · primary action
    _FilterChips      All / … with counts (links, so the filter is in the URL)
    .tbx-list         one .tbx-row per item
    _EmptyState       when the user has nothing yet
    .tbx-shortcuts    "/ search · N new …"
</div>
<div class="modal" data-list-modal>  the create form
```

| Piece | Files | Notes |
|---|---|---|
| Toolbar | `Views/Shared/_ListToolbar.cshtml`, `ListToolbarViewModel` | Summary line, GET search box (keeps the active filter through `SearchKeep`), primary button that opens a modal. No page title: the layout header already shows it. |
| Filter chips | `Views/Shared/_FilterChips.cshtml`, `FilterChipsViewModel`, `FilterChipViewModel` | Single choice. Each chip is a link with a count; the active one has `aria-current="page"`. Counts follow the current search. |
| List | `.tbx-list`, `.tbx-row`, `.tbx-row--two-line`, `.tbx-row-main`, `.tbx-row-title`, `.tbx-row-sub`, `.tbx-row-meta`, `.tbx-row-check`, `.tbx-row-actions` in `Content/css/components.css` | Dense rows (36px, two lines when there is a description). A checkbox at the start is a `<button role="checkbox" aria-checked>` that submits its own form. Row actions show on hover and keyboard focus with a mouse, always on touch screens. Status uses `.tbx-pill` (danger = overdue, warning = due today). |
| Empty states | `Views/Shared/_EmptyState.cshtml` for "nothing yet" (with the primary action); an inline `.tbx-empty` with "Clear filter" when nothing matches | Two different sentences: one invites, the other explains the filter. |
| Form | A Bootstrap modal marked `data-list-modal` | Visible labels (`.tbx-field-label`), example placeholders, a reserved line for each error or hint (`.tbx-field-error`, `.tbx-field-hint`). On a failed submit the controller re-renders the page and sets `data-open-on-load="true"` so the modal reopens with the messages. |
| Dates | `<input data-date-picker="#hidden-id">` + `<input type="hidden" name="…">` | Kendo DatePicker in the UI culture's format; the hidden field always posts `yyyy-MM-dd`. Without Kendo it falls back to `<input type="date">`. |
| Delete | `data-confirm` on the submit button | The shared confirm modal names the item. |
| Behavior | `Content/js/list-page.js` | Moves `data-list-modal` dialogs to `<body>` (the app shell has its own z-index), `/` focuses search, `N` opens the primary modal, reopens a failed form, wires date pickers. |

## Server side

- The filter and search are query parameters (`?filter=overdue&search=report`). The service returns the items for the filter and the count behind every chip (`ChecklistService.GetListAsync`), always scoped to the user.
- Optional, as the to-do list does since stage 5.6: a sort order in the query string (remembered in a cookie), items grouped under small headings, a quick-add form above the list, rows that are focusable for keyboard shortcuts, a "select several" mode with a floating action bar, and "Undo" after complete or delete (the row is soft-deleted for a day, `ChecklistItem.DeletedAt`).
- Write actions (toggle, delete, create) post the current `filter` and `search` back and redirect to the same list, so the user stays where they were.
- "Today" comes from `AppClock.Today` (the app time zone), not the server clock.
- Validation messages are localization keys (`Validation.Required`, `Validation.MaxLength`).

## Checklist for a new list screen

1. Toolbar, chips and rows use the partials and classes above; no page-specific copies.
2. Every text comes from `vi-VN.json` and `en-US.json`.
3. Empty, no-match, and validation states are all reachable and look calm.
4. Works at 1440px and 390px, light and dark, without horizontal scroll (`tests/ui`).
