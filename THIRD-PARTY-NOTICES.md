# Third-Party Notices

ToolboxWeb uses the third-party components listed below. Their copyright and license notices remain with the original files. The policy for adding or upgrading components is in `AGENTS.md` under "Third-Party License Policy".

Last verified: 2026-10-21, by reading each package's `LICENSE` file or SPDX expression. jsdiff and highlight.js were added on 2026-11-19 after reading their `LICENSE` files.

## Client-side assets (shipped under `src/ToolboxWeb.Web/Content/`)

| Component | Version | License | Location | Status |
|---|---|---|---|---|
| Bootstrap | 5.3.3 | MIT | `lib/bootstrap/` | In use |
| jQuery | 3.7.1 | MIT | `lib/jquery/` | In use |
| jQuery Validation | 1.21.0 | MIT | `lib/jquery-validation/` | In use |
| jQuery Validation Unobtrusive | 4.0.0 | Apache-2.0 | `lib/jquery-validation-unobtrusive/` | In use |
| Bootstrap Icons (subset) | — | MIT | `vendor/bootstrap-icons/` | In use |
| Lucide (`lucide-static`; sprite of the icons listed in `tools/lucide/icons.txt`, built by `tools/lucide/build-sprite.py`) | 1.60.0 | ISC (icons derived from Feather: MIT) | `vendor/lucide/` (with `LICENSE`) | In use (app icons) |
| Kendo UI Core (`kendo-ui-core`) | 2026.2.520 | Apache-2.0 | `vendor/kendo/js/`, `cultures/`, `messages/` | In use (Core widgets only) |
| Kendo Theme Default (`@progress/kendo-theme-default`) | 14.1.0 | Apache-2.0 | `vendor/kendo/theme/` | In use |
| Kendo Font Icons (`@progress/kendo-font-icons`) | 5.1.0 | Apache-2.0 | `vendor/kendo/icons/` | In use |
| Inter (`@fontsource/inter`, latin, latin-ext, vietnamese; 400/500/600/700) | 5.3.0 | OFL-1.1 | `vendor/fonts/inter/` (with `LICENSE`) | In use (app font) |
| Tabulator (`tabulator-tables`) | 6.3.1 | MIT | `vendor/tabulator/` | In use |
| Apache ECharts (`echarts`, `dist/echarts.min.js` from the npm package). The bundle includes ZRender 5.6.1 (BSD-3-Clause) and tslib 2.3.0 (0BSD), and embeds parts of d3 (BSD-3-Clause) | 5.6.0 | Apache-2.0 | `vendor/echarts/` (with `LICENSE`, `NOTICE`, `licenses/LICENSE-d3`) | In use (Jira Dashboard chart, through `js/charts.js`). The file shipped before 2026-10-21 was 5.5.1 although listed as 5.6.0; it was replaced with the official 5.6.0 build |
| FullCalendar Standard Bundle | 6.1.15 | MIT | `vendor/fullcalendar/` | Asset only, not wired. Premium plugins are commercial and must not be added |
| Quill | 2.0.2 | BSD-3-Clause | `vendor/quill/` | Asset only, not wired |
| jsdiff (npm `diff`, `dist/diff.min.js`; a one-line license comment was added at the top because the build has none) | 9.0.0 | BSD-3-Clause (LICENSE read in the npm package; © 2009-2015 Kevin Decker). No runtime dependencies | `vendor/jsdiff/` (with `LICENSE`) | In use (Compare text, `js/text-compare.js`) |
| highlight.js (`@highlightjs/cdn-assets`: `highlight.min.js` common-language build plus `languages/powershell.min.js`; no theme stylesheet, colours come from `tokens.css`) | 11.12.0 | BSD-3-Clause (LICENSE read in the npm package; © 2006 Ivan Sagalaev, Josh Goebel and contributors). No runtime dependencies | `vendor/highlightjs/` (with `LICENSE`) | In use (Compare text code colours) |
| SheetJS Community Edition (`xlsx.full.min.js`, includes the SheetJS codepage tables, Apache-2.0) | 0.20.2 | Apache-2.0 | `vendor/sheetjs/` (with `LICENSE`) | In use (Jira Search "Export to Excel"; loaded only when the button is pressed). The `LICENSE` text was taken from the npm package `xlsx@0.18.5`, the same Apache-2.0 text, because cdn.sheetjs.com could not be reached when it was added |

## NuGet packages (direct references in `ToolboxWeb.Web.csproj`)

| Package | Version | License | Status |
|---|---|---|---|
| Microsoft.AspNetCore.Diagnostics.EntityFrameworkCore | 9.0.6 | MIT | In use |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 9.0.6 | MIT | In use |
| Microsoft.AspNetCore.Identity.UI | 9.0.6 | MIT | In use. The pages under `Areas/Identity/Pages/Account/` (register, two-factor, manage) are adapted from its source (dotnet/aspnetcore, `src/Identity/UI`, MIT) and rewritten with localization keys |
| Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation | 9.0.6 | MIT | In use (Development) |
| Microsoft.EntityFrameworkCore.Sqlite | 9.0.6 | MIT | In use |
| Microsoft.EntityFrameworkCore.Tools | 9.0.6 | MIT | Design-time |
| Velopack | 1.2.161 | MIT (LICENSE read on github.com/velopack/velopack; © Caelan Sayler, Velopack Ltd.) | In use: installer hooks and in-app updates of the desktop copy (`Program.cs`, `Services/DesktopUpdateService.cs`). No dependencies on net9.0. The installer (Setup.exe, Update.exe) that `vpk` adds to the package is part of the same MIT project |
| Microsoft.Data.SqlClient | 6.1.5 | MIT | In use (Schema Compare, SQL Profiler) |
| Microsoft.SqlServer.DacFx | 170.4.83 | **Microsoft Software License Terms** (free, proprietary, redistributable with conditions) | In use (Schema Compare). **Approved by the owner on 2026-10-21**, see below |

## License server (`src/ToolkitLicense.Web`)

No new dependency. It references three packages already listed above, at the same versions: `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.EntityFrameworkCore.Tools` (design-time). It serves these files of `ToolboxWeb.Web/Content` through links in its csproj, under the same licenses: Bootstrap, Inter, the Lucide sprite, the Toolkit logo and the app's own CSS. Signing uses `System.Security.Cryptography` from .NET. The two-factor key is shown as text, so no QR code library is used.

## Transitive NuGet packages

All 138 packages in the restore graph were checked:

| Group | Count | License |
|---|---|---|
| Most packages | 86 | MIT |
| Older `System.*` packages that `dotnet/corefx` links to | 4 | MIT |
| `SQLitePCLRaw.*` | 4 | Apache-2.0 |
| Older `System.*` 4.x and `Microsoft.Build` 15.9 packages, pulled in by DacFx | 41 | Microsoft .NET Library License (`go.microsoft.com/fwlink/?LinkId=329770`) |
| `Microsoft.Data.SqlClient.SNI.runtime` 6.0.2 | 1 | Microsoft Software License Terms. Native network layer required by Microsoft.Data.SqlClient on Windows |
| `Microsoft.SqlServer.Types` 170.1000.7 | 1 | **Microsoft Pre-release Software License Terms (SQL Server vNext Community Preview)**. Required by DacFx 170.x. See below |
| `Microsoft.SqlServer.DacFx` 170.4.83 | 1 | Listed above |

## Owner decisions

**2026-10-21: the owner accepted all three Microsoft components below** (DacFx with its dependency chain, `Microsoft.SqlServer.Types`, and `Microsoft.Data.SqlClient.SNI.runtime`) for use in ToolboxWeb. Their license texts are kept as they are. Review this decision again when DacFx is upgraded, in case `Microsoft.SqlServer.Types` then ships with general-availability license terms.

The background for each decision:

1. **DacFx and its dependency chain** (Microsoft Software License Terms, Microsoft .NET Library License).
   - These are free Microsoft licenses, not open-source licenses. They allow use and distribution inside your own application, with conditions: no reverse engineering, and distributors must accept terms at least as protective. They do not require payment.
   - Under the policy in `AGENTS.md` they need explicit owner approval.
   - The only alternative would be dropping DacFx-based schema compare.
2. **`Microsoft.SqlServer.Types` 170.1000.7 ships a pre-release license file.**
   - The file includes: "You may not test the software in a live operating environment unless Microsoft permits you to do so under another agreement."
   - The version number looks like a GA release, so this may be a packaging mistake by Microsoft. As published, though, it conflicts with the policy's "no pre-release terms outside development" rule.
   - Options:
     - accept the risk, and record that here;
     - use a DacFx version whose `Microsoft.SqlServer.Types` dependency carries GA terms (needs checking);
     - keep Schema Compare as a development-only feature.
3. **`Microsoft.Data.SqlClient.SNI.runtime`** (Microsoft Software License Terms) is needed for any SQL Server connection from .NET on Windows. If SQL Server features stay, it stays. Approval is recommended.

## Explicitly not used

| Component | License | Reason |
|---|---|---|
| `@progress/kendo-drawing` | Commercial (Telerik/Progress EULA, trial terms without a license) | Forbidden. Kendo UI Core gets an empty placeholder global instead |
| Kendo UI commercial widgets (Grid, Scheduler, Chart, Editor, …) | Commercial | Forbidden. Not part of Kendo UI Core |
| Aspose (Aspose.Cells and others) | Commercial | Forbidden. `Infrastructure/Excel/AsposeExcelReportService.cs` only has the name; it writes CSV with no Aspose dependency |
| FullCalendar Premium plugins | Commercial | Forbidden |

## Dev-only tooling (not shipped)

| Component | License | Used by |
|---|---|---|
| Playwright (`@playwright/test`) | Apache-2.0 | `tests/ui/` |
| vpk (Velopack CLI, .NET tool) 1.2.161 | MIT | `tools/release/` (builds the desktop installer and update packages) |
| zstd (system tool, only for `publish-desktop.sh` on Linux/macOS) | BSD-3-Clause | delta packages when packaging outside Windows |
| mkcert (owner's tool, not in the repository) | BSD-3-Clause | HTTPS certificates for a LAN setup (`docs/LAN_SETUP.md`) |
