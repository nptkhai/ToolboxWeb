# Feature Prompts

These prompts are the source of truth for the built-in prompt templates. The `SeedPrompt.*` keys in `src/ToolboxWeb.Web/Localization/en-US.json` and `vi-VN.json` must match them (see "Prompt And Documentation Sync" in `AGENTS.md`).

Conventions:

- Variables use `{{variable_name}}` (English snake_case in every language).
- Every prompt starts with the shared context block, so it still works when pasted into a chat tool that cannot read the repository.
- Every prompt ends with the expected output format, so copying the prompt alone is enough.

## Shared Context Block

```text
Context: ToolboxWeb is a server-rendered ASP.NET Core MVC app on .NET 9 with SQLite + EF Core migrations, ASP.NET Identity, Razor views, Bootstrap 5, Kendo UI Core (Apache-2.0 widgets only, no commercial Kendo packages), and Tabulator. The web root is Content/ (not wwwroot). User-facing text comes from Localization/vi-VN.json and Localization/en-US.json. Controllers stay thin, business logic lives in Services/, and every user-owned query filters by UserId. If you can read the repository, read AGENTS.md and docs/ARCHITECTURE.md first.
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

Constraints: follow the module order in AGENTS.md (Domain, ViewModels, service interface and implementation, registration in ServiceCollectionExtensions, controller, Razor views, activity logging, migration). Follow the UI Standard in AGENTS.md and use Views/Notes/Index.cshtml as the reference list screen. Do not add dependencies outside the license policy.

Respond with:
1. Solution summary
2. Domain entity and persistence shape
3. Service methods
4. Controller actions
5. ViewModels and Razor views (including empty, loading and error states)
6. Localization keys to add (vi-VN and en-US)
7. Migration impact and data compatibility risk
8. Build and verification steps
```

## UI Change (seed: UiChange)

Variables: `{{screen}}`, `{{goal}}`, `{{areas_to_change}}`, `{{known_pain_points}}`

```text
[Shared context block]

I want to change the UI of screen: {{screen}}.
Goal: {{goal}}.
Areas to change: {{areas_to_change}}.
Known pain points: {{known_pain_points}}.

Follow the UI Standard in AGENTS.md:
- Design direction inspired by Linear: neutral surfaces, one accent color, color only for status and feedback, dense lists, roomy forms. Do not copy Linear's logo, icons, colors or CSS.
- Reuse the CSS variables in :root; add no new color literals.
- One page title (topbar), visible labels on every field, confirmation modal for deletes and external writes, empty/loading/error states, minimum 12px text, contrast at least 4.5:1, no text directly on the decorative background.
- Localization keys in both vi-VN and en-US; numbers and dates follow the UI culture.
- Grid convention: center header labels, left-align text, right-align decimal and money, center the rest.
- Works at 1440px and 390px wide, light and dark, with no horizontal scroll.

Respond with:
1. Main UI changes
2. Razor, CSS and JS files to update
3. Shared components reused or added
4. Responsive, dark mode and localization impact
5. Verification steps (tests/ui check, screens and widths to review)
```

## Database Change (seed: DatabaseChange)

Variables: `{{reason}}`, `{{affected_entities}}`, `{{data_to_preserve}}`

```text
[Shared context block]

I need to change the SQLite schema because: {{reason}}.
Affected entities: {{affected_entities}}.
Existing data that must be preserved: {{data_to_preserve}}.

If the change touches ApplicationUser or tables read by built-in Identity pages, include how to confirm the runtime SQLite file is updated.

Respond with:
1. Entity and ApplicationDbContext changes
2. Migration changes
3. Data compatibility risk
4. User isolation impact
5. Build, migration and verification commands
```

## UX And Accessibility Review (docs only, not seeded yet)

Variables: `{{screens}}`, `{{viewports}}`

```text
[Shared context block]

Review the UX and accessibility of: {{screens}}, at viewports {{viewports}}, in light and dark themes.

Check against the UI Standard in AGENTS.md: single page title, visible labels, confirmation for destructive or external actions, empty/loading/error states, minimum font size, contrast, keyboard access and focus visibility, active navigation state, localization (no mixed languages, no developer jargon), number and date formats, horizontal overflow on mobile.

Respond with:
1. Findings ordered by severity (data loss or misleading > blocked task > wrong display > cosmetic), each with screen, element and file
2. Suggested fix per finding
3. What could not be verified and why
```
