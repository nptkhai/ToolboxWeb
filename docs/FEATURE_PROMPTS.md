# Feature Prompts

## New Toolbox Module

```text
Read docs/ARCHITECTURE.md first.
I want to add a new ToolboxWeb module named [name].
User goal: [goal].
Data to store: [fields].
Main screens: [screens].
Actions: [create/update/delete/export/etc].
Constraints:
- keep controllers thin
- put business logic in Services
- persist through ApplicationDbContext
- filter user data by UserId
- preserve Content/ as the web root
- keep UI-only preferences in localStorage
Please respond with:
1. Feature summary
2. Domain entity and persistence shape
3. Service methods
4. Controller actions
5. ViewModels and Razor views
6. Migration impact and data compatibility risk
7. Tests or build commands to run
```

## UI Change

```text
Read docs/ARCHITECTURE.md first.
I want to change the UI of [screen].
Keep the compact dashboard style. Do not add a marketing landing page.
Preserve Content/ as the web root and keep UI preferences in localStorage.
Important context: [desktop/mobile behavior, existing pain points, specific sections].
If the screen contains a grid/table, specify alignment rules explicitly.
Default ToolboxWeb grid convention:
- center all header labels
- left-align text columns in the body
- right-align decimal or money columns in the body
- center-align the remaining body columns
Please respond with:
1. Main UI changes
2. Razor/CSS/JS files to update
3. Responsive considerations
4. Any localStorage or localization impact
5. Verification steps
```

## Database Change

```text
Read docs/ARCHITECTURE.md first.
I need to change SQLite schema for [reason].
Affected entities: [entities].
Existing data constraints: [requirements].
Explain:
1. Entity and DbContext changes
2. Migration changes
3. Data compatibility risk
4. User isolation impact
5. Commands/tests to verify the migration
```
