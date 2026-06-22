# Maintenance Prompts

Use these prompts when asking an AI assistant or onboarding a new maintainer.

## Feature Change

```text
You are maintaining an ASP.NET Core MVC app. Read docs/ARCHITECTURE.md first.
Implement this feature: [feature].
Keep controllers thin, put business logic in Services, persist data through ApplicationDbContext, and keep user data filtered by UserId.
Constraints or non-goals: [list].
Respond with:
1. Short implementation plan
2. Files changed
3. Migration or data impact
4. Tests or build commands to run
```

## Bug Fix

```text
Read docs/ARCHITECTURE.md and inspect the module: [module].
Bug: [bug].
Steps to reproduce: [steps].
Expected result: [expected].
Actual result: [actual].
Current user/data context: [context].
Find the smallest correct fix, avoid unrelated refactors, and mention any migration or user isolation impact.
```

## Code Review

```text
Review this change as a senior ASP.NET Core MVC engineer.
Prioritize bugs, security, user isolation, validation, migration safety, and missing tests.
Give file/line findings first. If no issues are found, state remaining test gaps.
```

## Handoff

```text
Summarize module [module] for a new maintainer:
purpose, controller, service, domain entities, view models, Razor views, data flow, risks, and verification checklist.
```
