# ToolboxWeb Agent Guide

## ToolboxWeb Overview

- ToolboxWeb is an ASP.NET Core MVC app on .NET 9 with SQLite, EF Core migrations, ASP.NET Identity, Razor views, and static assets served from `Content/`.
- Read these files first when they are relevant:
  - `README.md` for run/build and source layout.
  - `docs/ARCHITECTURE.md` for layering, storage rules, and module flow.
  - `src/ToolboxWeb.Web/Program.cs` for runtime conventions such as `Content/` as web root and localization setup.
  - `src/ToolboxWeb.Web/Constants/ToolboxRouteSlugs.cs` for canonical page and tab slugs.
- This repo is server-rendered MVC, not a SPA. Prefer thin controllers, service-driven behavior, and Razor-first changes.

## CodeGraph Rules

- Repo-local rule: there is no `.codegraph` directory in the `ToolboxWeb` git root, so do not assume repo-local CodeGraph is available.
- Workspace note (machine-specific): on the maintainer's Windows workstation, a shared CodeGraph directory may exist at the parent workspace level, currently `D:\Source_Toolbox\.codegraph`. Other machines, CI, and cloud sessions will not have it.
- If CodeGraph tooling is available and usable, prefer it before broad manual searching.
- If CodeGraph CLI or tools fail, for example with `unable to open database file`, fall back immediately to normal shell search and file reads.
- Never block work on CodeGraph availability.

## Architecture Rules

- Controllers should stay thin: receive the request, validate `ModelState`, call services, and return a view, redirect, or JSON result.
- Business logic belongs in `src/ToolboxWeb.Web/Services/`.
- Domain entities in `src/ToolboxWeb.Web/Domain/` describe persisted concepts and should not absorb UI logic.
- View models in `src/ToolboxWeb.Web/ViewModels/` shape form input and page state for Razor views.
- Technical integrations belong under `src/ToolboxWeb.Web/Infrastructure/`.

## Data And Persistence Rules

- Every user-owned entity must be filtered by `UserId` in services and queries.
- When adding a new module, follow this order:
  - add or update `Domain` entities when persistence is needed,
  - add `ViewModels`,
  - add service interface and implementation,
  - register the service in `Extensions/ServiceCollectionExtensions.cs`,
  - add controller actions,
  - add or update Razor views,
  - add activity logging for important create, update, delete, complete, or use actions,
  - add a migration if schema changed.
- When changing schema, update both the entity model and `ApplicationDbContext`, and explicitly consider migration compatibility risk.
- If a schema change touches `ApplicationUser` or any table read by built-in ASP.NET Identity pages, verify the actual runtime database file is updated before considering the task done.
- For identity-facing schema additions, do not validate only the custom page you edited. Also test the built-in tabs that still query the same user record, especially `Manage/Index`, `Manage/Email`, `Manage/ChangePassword`, `Manage/TwoFactorAuthentication`, and `Manage/PersonalData`.
- Do not bypass service-layer user isolation by querying DbContext directly from views or controllers.
- Treat the configured `Storage` paths as runtime-owned data. In Production, keep the SQLite database, uploaded files, and Data Protection keys outside the published website directory.
- Serve `/uploads` from the configured external uploads directory; do not add user-uploaded files back into `Content/` or the publish package.
- Keep `app.db` and `Content/uploads/**` excluded from publish so deployments cannot overwrite or delete user data.

## Routing, Localization, Encoding

- Public pages should follow the `.html` route convention used by the current source.
- Use `ToolboxRouteSlugs` for canonical page and tab routes instead of scattering hardcoded URLs.
- `Content/` is the real web root. Do not assume `wwwroot/`.
- User-facing text should prefer localization keys in:
  - `src/ToolboxWeb.Web/Localization/vi-VN.json`
  - `src/ToolboxWeb.Web/Localization/en-US.json`
- Avoid pasting long inline Vietnamese literals directly into `cshtml` when a localization key is appropriate.
- If text appears mojibake or broken, fix it with clean UTF-8 content or localization keys instead of copying the broken text forward.
- Preserve the file's existing line-ending style when editing. For repo text files that are already Windows-style, keep `CRLF` and do not leave mixed `CRLF/LF` endings after partial edits.
- After touching localization or other text-heavy files such as `.json`, `.cshtml`, `.css`, and `.js`, normalize line endings before finishing if the editor/tool introduced mixed endings.

## Identity UI Rules

- `Areas/Identity/Pages/Account/Manage/` is a shared surface that mixes local pages and package-provided pages. Do not customize only one page and assume the rest of the folder will visually match.
- Before changing any page inside `Account/Manage`, inspect the folder-level `_Layout.cshtml`, `_ViewStart.cshtml`, nav behavior, and whether package pages in the same folder will inherit the same wrapper.
- If you introduce a custom manage page layout, make it folder-wide so tab switches do not jump between different wrappers or duplicate navigation blocks.
- When you add profile or account UI that reads user data, make sure it degrades safely if the current runtime database has not yet picked up the newest schema.
- Every Identity page the app shows is a local page under `Areas/Identity/Pages/Account/` (sign-in, register, two-factor sign-in, recovery code, lockout, and all `Manage/` pages), written in localization keys. Do not fall back to the package's English pages. Manage pages derive from `Manage/ManagePageModel` and render inside `Manage/_Layout.cshtml`.
- Jira accounts have no ToolboxWeb password: the password and two-factor tabs are hidden for them.
- No email sender is configured, so there is no "forgot password" and the email cannot be changed. Do not add flows that depend on sending email until a sender exists.
- Deleting an account goes through `IAccountService.DeleteAccountAsync`, which also deletes every user-owned row and the profile images. A new user-owned table must be added there.

## Third-Party License Policy

- ToolboxWeb must not depend on commercial, trial, or paid-license code. This applies to everything that ships with the app: NuGet packages, files under `Content/lib/` and `Content/vendor/`, fonts, icons, and images.
- License tiers:
  - **Allowed:** MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, SIL OFL 1.1 (fonts only).
  - **Owner approval required** (record the decision in `THIRD-PARTY-NOTICES.md` before merging): MPL-2.0, LGPL, any "free but proprietary" vendor license such as Microsoft Software License Terms, and any package whose license is a file instead of an SPDX expression.
  - **Forbidden:** commercial or trial licenses (for example `@progress/kendo-drawing`, Kendo UI for jQuery commercial widgets, Telerik, DevExpress, Syncfusion, Aspose), GPL/AGPL for shipped code, and pre-release or preview license terms used outside development.
- Before adding or upgrading any dependency:
  - read the actual `LICENSE` file of the package, not only the registry summary;
  - check its dependencies too. An Apache-2.0 package can still require a commercial package at runtime, as Kendo UI Core does with `@progress/kendo-drawing`;
  - add or update the entry in `THIRD-PARTY-NOTICES.md` (name, version, license, location) in the same change.
- Keep the copyright and license headers inside vendored `.min.js` and `.css` files.
- Kendo UI Core needs the global `window._progress_kendo_drawing` to exist before `kendo.ui.core.min.js` loads. Provide an empty placeholder object written in this repo. Never load `@progress/kendo-drawing` itself. Kendo features that need it (ColorPicker, PDF export, drawing) are not available; use a native `<input type="color">` for color input.
- Copying design ideas is fine. Copying another product's logo, name, icon set, illustrations, brand colors, or CSS and asset files is not. This applies to the Linear-inspired direction in the UI Standard below.
- Dev-only tooling (for example Playwright under `tests/ui/`) does not ship with the app, but should still use allowed licenses. Ask before adding tooling that needs owner approval.

## Frontend Library Rules

- Bootstrap is the default base UI layer.
- Kendo UI Core is the widget layer used when richer client behavior is needed.
- Tabulator is the editable grid solution currently in use.
- Asset-present libraries such as `ECharts`, `FullCalendar`, `Quill`, and `SheetJS` should be treated as available assets, not automatically as wired, supported live integrations.
- For Kendo UI Core, only live-use widgets that are confirmed usable in the current local bundle.
- If a Kendo widget is missing from the current bundle, use a catalog note or fallback pattern instead of initializing it blindly.
- Shared frontend controls must be instance-scoped. Do not rely on global IDs or one-off selectors when the same control can appear multiple times on one screen.
- For reusable upload-like controls, keep JavaScript queries relative to the control root and require unique `InputName` and removed-state field names per instance so add, update, and delete flows do not leak across controls.
- SQL Server connection forms use `Views/Shared/_SqlConnectionForm.cshtml` with `SqlConnectionFormViewModel` and `Content/js/sql-connection.js` (`ToolboxSqlConnection.init(panel, options)`; `read`, `set`, `test`, `setStatus`). Pages add their own rules through `onTested`, `onChange`, and `beforeTest` instead of copying the form.
- Jira screens share `Content/js/jira-common.js` (`window.ToolboxJira`): ISO period dates, status pills (`statusPill`; server side `JiraStatus.PillClass`), Tabulator defaults and formatters (`grid.options`, `grid.formatters`), the period picker (`Views/Jira/_JiraPeriodPicker.cshtml` + `periodPicker`), the filter chip bar (`chipBar`) and the JQL block (`Views/Jira/_JiraJqlBlock.cshtml` + `jql.update`). New Jira screens use these instead of copying date, status, or picker code. Load it after `format.js` and before the page script.

## Preferred Controls

- This is the current preferred control set for ToolboxWeb. Extend this list later if the control strategy changes.
- `Native Bootstrap Text Input` - `Bootstrap + HTML`
- `Native Textarea` - `Bootstrap + HTML`
- `Kendo AutoComplete` - `Kendo UI Core`
- `Native Checkbox` - `Bootstrap + HTML`
- `Kendo ComboBox` - `Kendo UI Core`
- `Kendo MultiSelect` - `Kendo UI Core`
- `Kendo DatePicker` - `Kendo UI Core`
- `Kendo NumericTextBox` - `Kendo UI Core`
- `Tabulator Grid Demo` - `Tabulator`
- `Popup` - `Bootstrap Modal`
- `Bootstrap Tabs` - `Bootstrap`
- `Bootstrap Alerts` - `Bootstrap`
- `Accent color` - `Native color input` with `Bootstrap + HTML` wrapper
- Treat this as the main control set for new work. Do not assume bundle-missing widgets are part of the preferred set.

## Grid Convention Rules

- Center all header labels.
- Left-align text columns in the body.
- Right-align decimal or money columns in the body.
- Center-align the remaining body columns.
- Preserve these conventions in Tabulator demos and in any new table or grid-like UI unless the screen has an explicit exception.

## UI Standard

- Review findings and the agreed roadmap live in `docs/UI_UX_REVIEW.md` (section 10 is the official order of work). Follow that order unless the owner says otherwise.
- Design direction: inspired by Linear's principles, not a copy of it.
  - Neutral surfaces with one accent color; color is reserved for status, priority, and feedback.
  - Dense lists, roomy forms.
  - Calm empty states and subtle motion.
  - Keyboard-friendly.
  - Concrete values were approved on 2026-10-21 and are listed under "Design Tokens" below. The reasons are in `docs/DESIGN_DECISIONS.md`.
- Reference screen: `Views/Notes/Index.cshtml` (toolbar + search + primary action + empty state + modal confirm). New or reworked list screens follow its structure. The reusable form of that structure is the list page template in `docs/LIST_PAGE_TEMPLATE.md` (`_ListToolbar`, `_FilterChips`, `.tbx-list` rows, `_EmptyState`, a `data-list-modal` form, `Content/js/list-page.js`); the to-do list is its first user.
- Rules that apply now:
  - **Colors:** do not add new hex, rgb, or hsl color literals in CSS or inline styles. Reuse the CSS variables in `:root`. If a needed color does not exist, add one variable to `:root` (light and dark) instead of a literal.
  - **One primary button style** across the app. Destructive actions use the danger style.
  - **One page title per screen,** rendered by the layout topbar. Do not repeat it as an `<h1>` inside the content.
  - **Forms:** every input, select, and textarea has a visible `<label>`. Placeholders are examples only, never the label. Reserve space for validation messages so the layout does not jump.
  - **Confirmation:** every delete, and every write to an external system (Jira, SQL Server), asks for confirmation in a Bootstrap modal that names the item. Buttons must not say "Save" when nothing is persisted; say "Apply" and show an unsaved-changes state instead.
  - **States:** every list or data area has an empty state (icon + one sentence + primary action when relevant), a loading state, and an error state.
  - **Readability:** text never sits directly on the decorative background; it sits on a surface. Minimum font size is 12px. Body text contrast is at least 4.5:1 in light and dark themes.
  - **Navigation:** the current page is marked in the sidebar with an active class and `aria-current="page"`.
  - **Language:** user-facing text comes from localization keys in both `vi-VN.json` and `en-US.json`. Do not mix Vietnamese and English inside one label. Keep developer jargon (source, bundle, input file, grid) out of user-facing text.
  - **Numbers and dates** follow the current UI culture (`17,5` in `vi-VN`). Use one date format per culture across the app: `DisplayFormatExtensions` (`ToDisplayDate`, `ToDisplayAppDateTime`, `ToDisplayNumber`, `ToDisplayHours`) on the server and `Toolbox.format` (`Content/js/format.js`) in the browser. "Today" and stored UTC times go through `AppClock` (`App:TimeZone`), never `DateTime.Today`/`DateTime.Now` for anything the user sees.
  - **Terms:** Jira and DB vocabulary follows `docs/GLOSSARY.md` (which Jira words stay as they are, which are translated).
  - **Third-party widgets:** Kendo and Tabulator widgets must look like native app controls. Style them through `Content/css/vendor-sync.css` using the shared variables, not per page.
  - **Responsive:** every changed screen works at 1440px and 390px wide, with no horizontal page scroll, in light and dark themes.

## Design Tokens

Approved by the owner on 2026-10-21 (`docs/DESIGN_DECISIONS.md`). They live in `Content/css/tokens.css`, the only stylesheet that declares raw color values for the app. How to use them:

- Read the variables (`var(--accent)`, `var(--space-3)`, …). Older page variables (`--ui-*`, `--jira-*`, `--nv-*`, `--sc-*`) are aliases onto them; do not add new aliases.
- Derived tokens in the same file: `--accent-tint`, `--success-tint`, `--warning-tint`, `--danger-tint`, `--line-strong`, `--hover`, `--focus-ring`, `--shadow`, `--shadow-popup`, `--on-danger`.
- Shared building blocks are in `Content/css/components.css` (`.tbx-panel`, `.tbx-toolbar`, `.tbx-list`/`.tbx-row`, `.tbx-pill`, `.tbx-chip`, `.tbx-field`, `.tbx-sidepanel`, `.tbx-empty`, `.tbx-error-state`, `.tbx-skeleton`). Empty states use `Views/Shared/_EmptyState.cshtml` with `EmptyStateViewModel`.
- Icons: `<tbx-icon name="search" />` (add `label="…"` when the icon stands alone). The sprite only holds the names in `tools/lucide/icons.txt`; to add one, add the name and rebuild with `tools/lucide/build-sprite.py`.

- **Accent** (owner's choice). Use each token only for its listed purpose; the brand shades are too light for text in light mode.

  | Token | Light | Dark | Use |
  |---|---|---|---|
  | `--accent-brand` | `#4B87BB` | `#8ED3F2` | decoration only: charts, progress bars, logo, accent borders |
  | `--accent` | `#417BAD` | `#8ED3F2` | primary buttons, links, focus outline |
  | `--accent-strong` | `#3A6E9B` | `#9FDAF4` | text on accent tints (status pills, active nav, selected chips); primary button hover |
  | `--on-accent` | `#FFFFFF` | `#0E1014` | text on accent-filled buttons |

- **Neutrals and status:**

  | Token | Light | Dark |
  |---|---|---|
  | `--bg` | `#F7F8FA` | `#0E1014` |
  | `--surface` | `#FFFFFF` | `#15181E` |
  | `--surface-2` | `#F2F4F7` | `#1C2028` |
  | `--line` | `#E3E6EB` | `#2A2F39` |
  | `--text` | `#16181D` | `#E8EAEE` |
  | `--muted` | `#5B6270` | `#A2A9B6` |
  | `--success` | `#15803D` | `#4ADE80` |
  | `--warning` | `#B45309` | `#FBBF24` |
  | `--danger` | `#C62828` | `#F87171` |

  Status tints are the status color mixed 12–14% into the surface with `color-mix()`, never a new literal.
- **Type:** Inter, self-hosted from `@fontsource/inter` (SIL OFL 1.1), weights 400/500/600 (700 for the logo only).
  - Sizes in rem: `--text-xs` 12px, `--text-sm` 13px, `--text-md` 14px, `--text-lg` 16px, `--text-xl` 20px, `--text-2xl` 24px.
  - Nothing smaller than 12px.
- **Spacing:** `--space-1…6` = 4/8/12/16/24/32px.
- **Radius:** 4px (pills), 6px (buttons, inputs), 8px (panels), 999px (chips).
- **Sizes:** list row 36px by default (44px in comfortable density), form input 38px, button 34px, header 48px, sidebar 232px.
- **Breakpoints:** 600 / 900 / 1200px.
- **Icons:** Lucide (ISC), only the icons actually used, as an SVG sprite under `Content/vendor/lucide/`. Kendo font icons only inside Kendo widgets.
- **Controls:** Bootstrap, Tabulator, and Kendo UI Core limited to DatePicker, ComboBox, MultiSelect, AutoComplete, NumericTextBox, Notification. ECharts, FullCalendar, Quill and SheetJS are wired only when a screen needs them.
- **App shell:**
  - Sidebar groups JIRA / CÁ NHÂN / CÔNG CỤ DB / DEV. DEV shows only in Development or for an authorized role.
  - 48px header with breadcrumb and page actions. Theme, language and sign-out live in the avatar menu.
  - Pages before sign-in (sign-in, register): no sidebar; the header shows the brand, clock, weather, language and theme; the animated weather background is always on (owner decision 2026-10-21, `DESIGN_DECISIONS.md` 2.H).
  - Inside the app: the header also shows the clock and weather (`_EnvironmentSummary`); the animated background is an opt-in setting on the account page, off by default. The browser asks for location once, for the weather.
  - The avatar menu trigger shows the profile photo plus the user's initials; without a photo, the initials sit in the circle.
  - First visit follows the OS light/dark preference; after that the user's choice is remembered.
- **Time and formats:** one `App:TimeZone` setting (default `Asia/Ho_Chi_Minh`), used by the server and the client clock. Formats follow the UI culture (`vi-VN`: `dd/MM/yyyy`, `HH:mm`, `17,5`).

## Prompt And Documentation Sync

- Built-in prompt templates exist in three places that must stay in sync:
  - `docs/FEATURE_PROMPTS.md` and `docs/MAINTENANCE_PROMPTS.md` (source of truth, English);
  - `SeedPrompt.*` keys in `Localization/en-US.json`;
  - `SeedPrompt.*` keys in `Localization/vi-VN.json`.
- When changing one, update the others in the same change.
- Prompt variables use the `{{variable_name}}` form, in English snake_case in every language. List the same names in `InputVariables`.
- A seeded prompt's `Content` must be self-contained: include the stack context and the expected output format. The copy button copies only `Content`.
- `Data/SeedData.cs` only inserts a seeded prompt when the user has no prompt in that category. Changing seed text therefore does not update existing users. Mention this in the change summary.

## Components Hub Appendix

- Files: `src/ToolboxWeb.Web/Views/Components/Index.cshtml`, `_Foundations.cshtml`, `_Catalog.cshtml`; styles in `Content/css/component-hub.css`, catalog filter in `Content/js/component-hub.js`.
- The hub is the style guide for developers. It, `DemoKendo` and `TabulatorDemo` carry `[DevToolsOnly]`: open in Development or to the `Developer` role, 404 elsewhere. The sidebar DEV group uses the same check (`Infrastructure/Security/DevToolsAccess.cs`).
- `ViewModels/ComponentCatalog.cs` is the single source for component status and license. The catalog tab and every demo card pill (`<tbx-component-status key="…" />`) read it. When you add a demo or change a component's status, update the catalog, not the markup. Statuses: Usable (preferred control set), Available (shipped but not adopted), CommercialDependency, NotInKendoCore.
- The `Component` page is grouped by component family, not by library.
- Add live demos only to the matching family tab. Do not place a demo in a nearby tab just because it reuses the same library.
- Family mapping:
  - `Foundations` (default tab): tokens, type, buttons, form field, pills, chips, list rows, empty/error/loading states, confirm, side panel, icons.
  - `Text Input`: text input, textarea, textbox-like inputs, autocomplete, combo box.
  - `Selection`: checkbox, select, dropdown list, multiselect, listbox, pick-list selection.
  - `Date, Time, Numeric`: date/time pickers, numeric input, slider, masked numeric/date-like input.
  - `Grid & Data List`: Tabulator grid, Kendo DataSource/ListView/Pager, list-data demos.
  - `Feedback & Popup`: alert, notification, validator, modal/window, tooltip, popup CRUD flows.
  - `Navigation & Layout`: menu, panelbar, splitter, layout and navigation demos.
  - `Catalog`: inventory, license and status only, not live demos.
- The Bootstrap CRUD popup demo belongs under `Feedback & Popup`.
- Its data target is the Tabulator demo inside `Grid & Data List`.
- Keep visible trigger IDs unique so JavaScript does not bind to hidden or duplicate nodes.

## Verification Checklist

- Backend change: run `dotnet build`.
- Schema change: run `dotnet build` and review the migration impact.
- Build verification artifacts must go to a temp path outside the repo, or the repo must ignore them first. Do not leave ad hoc verification output directories inside the working tree.
- Route or UI change: check canonical route behavior, localization impact, and asset loading.
- Kendo, Tabulator, or Components changes:
  - verify the widget really exists in the local bundle before marking it usable. A widget that is in the bundle but missing at runtime usually means the bundle stopped loading early; check the browser console first,
  - keep runtime behavior aligned with any "usable now" or bundle status labels,
  - confirm the demo sits in the correct component family tab,
  - confirm JavaScript bindings are not targeting duplicate IDs.
- Identity or profile changes:
  - confirm only one manage navigation shell renders,
  - confirm tab switches inside `Account/Manage` do not fall back to a different embedded layout,
  - confirm schema-backed fields used by account pages exist in the actual runtime SQLite file.
- UI change: run the UI check in `tests/ui/` (see `tests/ui/README.md`) against a locally running app, and compare the screenshots with the previous baseline.
- Jira screens cannot be checked without a Jira account. In Development, start the app with `Jira__UseFakeData=true` and sign in on the Jira tab with any URL, username, and password to get sample data. The owner still does the final check against the real Jira.
- License-affecting change: `THIRD-PARTY-NOTICES.md` is updated in the same change.

## Definition Of Done

A change is done only when all of these hold:

- `dotnet build` succeeds with no new warnings.
- For UI changes, the `tests/ui/` check reports no browser console errors and no horizontal overflow on the pages touched. New screenshots were reviewed against the baseline at 1440px and 390px, light and dark.
- The UI Standard rules above hold for every screen touched.
- Both `vi-VN.json` and `en-US.json` contain any new keys.
- `THIRD-PARTY-NOTICES.md` is current if a dependency changed.
- The change summary lists anything not verified. For example: "Jira screens checked with fake data only", or "existing users keep old seeded prompts".
