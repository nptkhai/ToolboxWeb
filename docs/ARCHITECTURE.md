# ToolboxWeb Architecture

## Overview

ToolboxWeb is an ASP.NET Core MVC app for personal productivity tools. It uses server-rendered Razor pages, SQLite persistence, Identity accounts, and small client-side JavaScript for UI preferences and timer behavior.

## Request Flow

```text
Browser
Controller
Service
ApplicationDbContext
SQLite
```

Controllers stay thin. Services own business rules, user isolation, and activity logging. Domain entities describe persisted concepts and do not know about UI details.

## Layers

- `Controllers/`: receive GET/POST requests, validate model state, call services, return views.
- `ViewModels/`: shape form input and page data for Razor views.
- `Services/`: execute module behavior, filter by current user, write activity history.
- `Domain/`: contains `ApplicationUser`, `Note`, `ChecklistItem`, `FocusSession`, `QuickLink`, `PromptTemplate`, `ActivityLog`.
- `Data/`: contains `ApplicationDbContext`, seed data, and design-time DbContext factory.
- `Migrations/`: contains EF Core migration history for SQLite schema changes.
- `Infrastructure/Excel/`: contains the shared Excel export abstraction. The current implementation emits CSV-compatible bytes and can be replaced by an Aspose-backed implementation later.
- `Content/`: configured as web root in `Program.cs` with `WebRootPath = "Content"`.

## Storage Rules

- SQLite stores user accounts, notes, checklist items, focus sessions, quick links, prompt templates, and activity history.
- `localStorage` stores UI-only preferences: `toolbox.theme` and `toolbox.sidebar`.
- Every user-owned entity has `UserId`. Services must always filter by `UserId`.

## Adding A New Tool

1. Add a domain entity if the tool needs persistence.
2. Add a ViewModel for forms/pages.
3. Add a service interface and implementation.
4. Register the service in `Extensions/ServiceCollectionExtensions.cs`.
5. Add a controller and Razor views.
6. Add activity logging for important create/update/delete/use actions.
7. Add or update EF migration in `Migrations/`.
