# Feature Prompts

## New Toolbox Module

```text
I want to add a new ToolboxWeb module named [name].
User goal: [goal].
Data to store: [fields].
Main screens: [screens].
Actions: [create/update/delete/export/etc].
Please propose the domain entity, service methods, controller actions, ViewModels, views, migration impact, and test cases.
```

## UI Change

```text
I want to change the UI of [screen].
Keep the compact dashboard style. Do not add a marketing landing page.
Preserve Content/ as the web root and keep UI preferences in localStorage.
```

## Database Change

```text
I need to change SQLite schema for [reason].
Explain the entity changes, migration changes, data compatibility risk, and commands to verify the migration.
```
