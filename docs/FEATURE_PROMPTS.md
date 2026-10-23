# Feature Prompts

These prompts are the source of truth for the built-in prompt templates. The `SeedPrompt.*` keys in `src/ToolboxWeb.Web/Localization/en-US.json` and `vi-VN.json` must match them (see "Prompt And Documentation Sync" in `AGENTS.md`).

Conventions:

- Variables use `{{variable_name}}` (English snake_case in every language).
- Every prompt starts with the shared context block, so it still works when pasted into a chat tool that cannot read the repository. The block names the design system and the shared helpers, so an assistant builds on them instead of inventing new styles.
- Every prompt ends with the expected output format, so copying the prompt alone is enough.
- Existing users keep the prompts they already have. On the prompt page, "Restore built-in prompts" adds the missing ones and, if the user ticks them, replaces changed ones with the current text.

## Shared Context Block

```text
Context: ToolboxWeb is a server-rendered ASP.NET Core MVC app on .NET 9 (SQLite + EF Core migrations, ASP.NET Identity, Razor views). The web root is Content/ (not wwwroot). Controllers stay thin, business logic lives in Services/, and every user-owned query filters by UserId.
Design system: colors and sizes come only from the tokens in Content/css/tokens.css (no color literals); shared building blocks are in Content/css/components.css (.tbx-panel, .tbx-toolbar, .tbx-list/.tbx-row, .tbx-pill, .tbx-chip, .tbx-field, .tbx-steps, .tbx-sidepanel, .tbx-empty, .tbx-error-state, .tbx-skeleton); list screens follow docs/LIST_PAGE_TEMPLATE.md (_ListToolbar, _FilterChips, _EmptyState, list-page.js); icons are Lucide through <tbx-icon> and Toolbox.icon.
Shared browser helpers: Toolbox.confirm (confirmation modal), Toolbox.notify, Toolbox.format (format.js) and Toolbox.keys (shortcuts.js; every keyboard shortcut is registered there). Widgets: Bootstrap 5, Kendo UI Core limited to DatePicker, ComboBox, MultiSelect, AutoComplete, NumericTextBox and Notification (no commercial Kendo), Tabulator for editable grids, Apache ECharts through charts.js.
Every page is listed once in ViewModels/AppNavigation.cs (sidebar, Ctrl+K command palette, "G then letter" shortcuts). User-facing text comes from Localization/vi-VN.json and Localization/en-US.json; dates and numbers follow the UI culture (AppClock, DisplayFormatExtensions, Toolbox.format).
If you can read the repository, read AGENTS.md first; docs/UI_UX_REVIEW.md records what each screen already does.
```

## New Toolbox Module (seed: FeatureRequest)

Variables: `{{feature_description}}`, `{{user_goal}}`, `{{scope_in_out}}`, `{{data_to_store}}`, `{{acceptance_criteria}}`

```text
[Shared context block]

I need a new feature: {{feature_description}}.
User goal: {{user_goal}}.
Scope (in / out): {{scope_in_out}}.
Data to store: {{data_to_store}}.
Acceptance criteria: {{acceptance_criteria}}.

Constraints:
- Follow the module order in AGENTS.md: Domain entity, ViewModels, service interface and implementation, registration in ServiceCollectionExtensions, controller, Razor views, activity logging, migration.
- A new user-owned table is filtered by UserId everywhere and also deleted in IAccountService.DeleteAccountAsync.
- A new page gets a ".html" route in ToolboxRouteSlugs and one entry in ViewModels/AppNavigation.cs; its shortcuts are registered with Toolbox.keys.
- Reuse the shared building blocks before writing new CSS or JS, and follow the UI Standard in AGENTS.md.
- No dependency outside the license policy (MIT, Apache-2.0, BSD, ISC; OFL for fonts).

Respond with:
1. Solution summary
2. Domain entity and persistence shape
3. Service methods
4. Controller actions
5. ViewModels and Razor views (including empty, loading and error states)
6. Shared components and helpers reused
7. Localization keys to add (vi-VN and en-US)
8. Migration impact and data compatibility risk
9. Build and verification steps (dotnet build, tests/ui at 1440px and 390px, light and dark)
```

## UI Change (seed: UiChange)

Variables: `{{screen}}`, `{{goal}}`, `{{areas_to_change}}`, `{{known_pain_points}}`

```text
[Shared context block]

I want to change the UI of screen: {{screen}}.
Goal: {{goal}}.
Areas to change: {{areas_to_change}}.
Known pain points: {{known_pain_points}}.

First read what docs/UI_UX_REVIEW.md records for this screen, so the change keeps what was already decided.

Follow the UI Standard in AGENTS.md:
- Inspired by Linear, not a copy: neutral surfaces, one accent color, color only for status and feedback, dense lists, roomy forms.
- Tokens only: no new color literals; status tints with color-mix().
- Reuse components.css, the list page template and the shared partials before adding page CSS; style Kendo and Tabulator only in vendor-sync.css.
- One page title (in the topbar), a visible label on every field, reserved space for validation messages, a confirmation modal naming the item for deletes and external writes, and "Apply" plus an unsaved state when nothing is saved yet.
- Empty, loading and error states; text at least 12px; contrast at least 4.5:1; no text directly on the decorative background.
- Shortcuts through Toolbox.keys with a label (the page line comes from data-shortcuts-line); no own keydown listener.
- Localization keys in vi-VN and en-US; dates and numbers through Toolbox.format or DisplayFormatExtensions.
- Grid convention: center header labels, left-align text, right-align decimal and money, center the rest.
- Works at 1440px and 390px wide, light and dark, without horizontal page scroll.

Respond with:
1. Main UI changes
2. Razor, CSS and JS files to update
3. Shared components and helpers reused or added
4. Responsive, dark mode, keyboard and localization impact
5. Verification steps (tests/ui check and new baseline, screens and widths to review)
6. The line to add to docs/UI_UX_REVIEW.md
```

## New Screen (seed: NewScreen, category UiChange)

Variables: `{{screen_name}}`, `{{purpose}}`, `{{menu_group}}`, `{{main_actions}}`, `{{data_source}}`

```text
[Shared context block]

I need a new screen: {{screen_name}}.
Purpose: {{purpose}}.
Menu group: {{menu_group}}.
Main actions: {{main_actions}}.
Data it shows or edits: {{data_source}}.

Build it the way the existing screens are built:
- Route: a ".html" route in ToolboxRouteSlugs; the title comes from ViewData["Title"] and appears only in the topbar.
- Menu: one entry in ViewModels/AppNavigation.cs with an icon, the group and a free "G" letter; that also puts it in the Ctrl+K palette and marks it active in the sidebar.
- Layout: docs/LIST_PAGE_TEMPLATE.md for a list, .tbx-steps for a 1-2-3 flow; forms in a Bootstrap modal (data-list-modal), details in .tbx-sidepanel.
- States: _EmptyState with an icon, one sentence and the primary action; a .tbx-skeleton while loading; .tbx-error-state with a retry.
- Writes: Toolbox.confirm naming the item for deletes and external writes; an activity log entry per important action; changes not saved yet show "Apply", an unsaved state and a warning before leaving.
- Shortcuts: "/" search and "N" new come from list-page.js; other keys through Toolbox.keys with a label, so they show in the "?" help and the page line.
- Text: keys in vi-VN.json and en-US.json; Jira and DB words as in docs/GLOSSARY.md; no developer jargon.
- Checks: dotnet build without warnings; the page added to tests/ui/pages.spec.js (and sample data to global-setup.js if it needs some); screenshots at 1440px and 390px, light and dark.

Respond with:
1. Screen structure (sections, controls, states)
2. Files to add or change (route, AppNavigation, controller, service, views, CSS, JS, tests/ui)
3. Shared components and helpers reused
4. Localization keys (vi-VN and en-US)
5. Keyboard shortcuts and their labels
6. Verification steps and the line to add to docs/UI_UX_REVIEW.md
```

## Database Change (seed: DatabaseChange)

Variables: `{{reason}}`, `{{affected_entities}}`, `{{data_to_preserve}}`

```text
[Shared context block]

I need to change the SQLite schema because: {{reason}}.
Affected entities: {{affected_entities}}.
Existing data that must be preserved: {{data_to_preserve}}.

Rules:
- Update the entity and ApplicationDbContext together, then add an EF Core migration; a new non-null column needs a default that suits the existing rows.
- SQLite rebuilds the table for some changes (foreign keys, dropped or retyped columns): say so and ask to back up app.db first.
- A new user-owned table filters by UserId everywhere and is deleted in IAccountService.DeleteAccountAsync.
- If the change touches ApplicationUser or tables read by built-in Identity pages, include how to confirm the runtime SQLite file is updated and which Account/Manage pages to open.

Respond with:
1. Entity and ApplicationDbContext changes
2. Migration changes (and whether SQLite rebuilds a table)
3. Data compatibility risk and backup advice
4. User isolation and account deletion impact
5. Build, migration and verification commands
```

## UX And Accessibility Review (docs only, not seeded yet)

Variables: `{{screens}}`, `{{viewports}}`

```text
[Shared context block]

Review the UX and accessibility of: {{screens}}, at viewports {{viewports}}, in light and dark themes.

Check against the UI Standard in AGENTS.md: single page title, visible labels, confirmation for destructive or external actions, empty/loading/error states, minimum font size, contrast, keyboard access and focus visibility, shortcuts registered with Toolbox.keys and listed in the "?" help, active navigation state, localization (no mixed languages, no developer jargon), number and date formats, color literals instead of tokens, horizontal overflow on mobile.

Respond with:
1. Findings ordered by severity (data loss or misleading > blocked task > wrong display > cosmetic), each with screen, element and file
2. Suggested fix per finding
3. What could not be verified and why
```
