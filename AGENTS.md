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
- Workspace note: a shared CodeGraph directory may exist at the parent workspace level, currently `D:\Source_Toolbox\.codegraph`.
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
- Do not bypass service-layer user isolation by querying DbContext directly from views or controllers.

## Routing, Localization, Encoding

- Public pages should follow the `.html` route convention used by the current source.
- Use `ToolboxRouteSlugs` for canonical page and tab routes instead of scattering hardcoded URLs.
- `Content/` is the real web root. Do not assume `wwwroot/`.
- User-facing text should prefer localization keys in:
  - `src/ToolboxWeb.Web/Localization/vi-VN.json`
  - `src/ToolboxWeb.Web/Localization/en-US.json`
- Avoid pasting long inline Vietnamese literals directly into `cshtml` when a localization key is appropriate.
- If text appears mojibake or broken, fix it with clean UTF-8 content or localization keys instead of copying the broken text forward.

## Frontend Library Rules

- Bootstrap is the default base UI layer.
- Kendo UI Core is the widget layer used when richer client behavior is needed.
- Tabulator is the editable grid solution currently in use.
- Asset-present libraries such as `ECharts`, `FullCalendar`, `Quill`, and `SheetJS` should be treated as available assets, not automatically as wired, supported live integrations.
- For Kendo UI Core, only live-use widgets that are confirmed usable in the current local bundle.
- If a Kendo widget is missing from the current bundle, use a catalog note or fallback pattern instead of initializing it blindly.

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

## Components Hub Appendix

- File: `src/ToolboxWeb.Web/Views/Components/Index.cshtml`
- The `Component` page is grouped by component family, not by library.
- Add live demos only to the matching family tab. Do not place a demo in a nearby tab just because it reuses the same library.
- Family mapping:
  - `Text Input`: text input, textarea, textbox-like inputs, autocomplete, combo box.
  - `Selection`: checkbox, select, dropdown list, multiselect, listbox, pick-list selection.
  - `Date, Time, Numeric`: date/time pickers, numeric input, slider, masked numeric/date-like input.
  - `Grid & Data List`: Tabulator grid, Kendo DataSource/ListView/Pager, list-data demos.
  - `Feedback & Popup`: alert, notification, validator, modal/window, tooltip, popup CRUD flows.
  - `Navigation & Layout`: menu, panelbar, splitter, layout and navigation demos.
  - `Catalog`: inventory and status only, not live demos.
- The Bootstrap CRUD popup demo belongs under `Feedback & Popup`.
- Its data target is the Tabulator demo inside `Grid & Data List`.
- Keep visible trigger IDs unique so JavaScript does not bind to hidden or duplicate nodes.

## Verification Checklist

- Backend change: run `dotnet build`.
- Schema change: run `dotnet build` and review the migration impact.
- Route or UI change: check canonical route behavior, localization impact, and asset loading.
- Kendo, Tabulator, or Components changes:
  - verify the widget really exists in the local bundle before marking it usable,
  - keep runtime behavior aligned with any "usable now" or bundle status labels,
  - confirm the demo sits in the correct component family tab,
  - confirm JavaScript bindings are not targeting duplicate IDs.
