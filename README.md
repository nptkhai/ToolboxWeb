# ToolboxWeb

ToolboxWeb is a personal productivity web app built with ASP.NET Core MVC, SQLite, ASP.NET Identity, and a compact dashboard-first interface.

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

## Source Layout

- `Controllers/`: request handling and redirects.
- `Domain/`: database entities.
- `Data/`: EF Core DbContext, migrations, seed data.
- `Services/`: business logic and user-scoped operations.
- `Infrastructure/`: technical integrations such as Excel export.
- `Enums/`, `Constants/`, `Extensions/`, `Helpers/`, `Shared/`: common code by responsibility.
- `ViewModels/`: form and page models.
- `Views/`: Razor UI.
- `Content/`: CSS, JavaScript, images, and frontend libraries.
- `docs/`: architecture and prompt guidance for maintainers.
