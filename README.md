# Toolkit

Toolkit (source repository: ToolboxWeb) is a personal productivity web app built with ASP.NET Core MVC, SQLite, ASP.NET Identity, and a compact dashboard-first interface. The code keeps the ToolboxWeb names; the brand assets are in `docs/brand/`, and the planned web/desktop/license-server model is in `docs/DEPLOYMENT_MODEL.md`.

## Current Stack

- ASP.NET Core MVC on .NET 9.0 SDK in this workspace.
- SQLite with Entity Framework Core migrations.
- ASP.NET Core Identity for user accounts.
- Razor Views with static assets served from `Content/` instead of the default `wwwroot/`.

The original plan targets .NET 10 LTS when the SDK is installed. This implementation uses `net9.0` because this machine currently has .NET SDK 9.0.301.

## Run

```powershell
dotnet restore
dotnet build
dotnet run --project src\ToolboxWeb.Web\ToolboxWeb.Web.csproj
```

Open the URL printed by `dotnet run`. Register a user, then the app opens the dashboard.

Desktop mode (one person's computer, data in `%LOCALAPPDATA%\Toolkit`, no sign-in, http://localhost:5180; see `docs/DEPLOYMENT_MODEL.md`):

```powershell
dotnet run --project src\ToolboxWeb.Web\ToolboxWeb.Web.csproj -- --desktop
```

A desktop with `License:PublicKey` set in `appsettings.Desktop.json` asks for a license from the license server first (see `docs/LICENSE_SERVER.md`); in Development without a key it checks nothing.

Web mode (the browser and phone site: Jira sign-in only, no database; see `docs/DEPLOYMENT_MODEL.md`):

```powershell
$env:App__Mode = "Web"
dotnet run --project src\ToolboxWeb.Web\ToolboxWeb.Web.csproj
```

License server (the owner's admin app on the main server: activation requests, licenses, devices, releases; see `docs/LICENSE_SERVER.md`). Runs on http://localhost:5190; on first start it writes a one-time setup code to `src/ToolkitLicense.Web/App_Data/setup-code.txt`:

```powershell
dotnet run --project src\ToolkitLicense.Web\ToolkitLicense.Web.csproj
```

## Source Layout

The solution has two web apps: `src/ToolboxWeb.Web` (Toolkit itself, described below) and `src/ToolkitLicense.Web` (the license server, which links the design files and a few source files of `ToolboxWeb.Web` instead of copying them).


- `Controllers/`: request handling and redirects.
- `Domain/`: database entities.
- `Data/`: EF Core DbContext, migrations, seed data.
- `Services/`: business logic and user-scoped operations.
- `Infrastructure/`: technical integrations such as Excel export.
- `Enums/`, `Constants/`, `Extensions/`, `Helpers/`, `Shared/`: common code by responsibility.
- `ViewModels/`: form and page models.
- `Views/`: Razor UI.
- `Content/`: CSS, JavaScript, images, and frontend libraries.
- `docs/`: architecture and prompt guidance for maintainers. `docs/UI_UX_REVIEW.md` holds the UI/UX review and the agreed roadmap.
- `tests/ui/`: Playwright UI check (console errors, overflow, labels, small text, screenshots). See `tests/ui/README.md`.

## Third-party licenses

See `THIRD-PARTY-NOTICES.md`, and the license policy in `AGENTS.md`. Commercial or trial-licensed packages, such as `@progress/kendo-drawing` or Aspose, are not allowed.
