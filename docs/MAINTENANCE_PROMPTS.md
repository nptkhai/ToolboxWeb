# Maintenance Prompts

Use these prompts when asking an AI assistant for help or when onboarding a new maintainer. They follow the conventions in `docs/FEATURE_PROMPTS.md`: `{{variable_name}}` placeholders, the shared context block, and an explicit output format. The seeded versions in `Localization/*.json` must match (see "Prompt And Documentation Sync" in `AGENTS.md`).

## Bug Fix (seed: BugReport)

Variables: `{{module}}`, `{{steps_to_reproduce}}`, `{{actual_result}}`, `{{expected_result}}`, `{{user_data_context}}`

```text
[Shared context block from docs/FEATURE_PROMPTS.md]

A bug happens in module: {{module}}.
Steps to reproduce: {{steps_to_reproduce}}.
Actual result: {{actual_result}}.
Expected result: {{expected_result}}.
User or data context: {{user_data_context}}.

Find the root cause, fix it in the right layer with the smallest correct change, and avoid unrelated refactors. If the bug sits in a shared part (components.css, vendor-sync.css, site.js, list-page.js, shortcuts.js, a shared partial), fix it there instead of patching one page, and list the other screens it affects. If the fix touches UI, follow the UI Standard in AGENTS.md.

Respond with:
1. Root cause
2. Proposed fix and files changed
3. Other screens that use the same code
4. User isolation, validation and migration impact
5. Tests to add or rerun (dotnet build, tests/ui), and what could not be verified
```

## Code Review (seed: CodeReview)

Variables: `{{change_to_review}}`

```text
[Shared context block from docs/FEATURE_PROMPTS.md]

Review this change: {{change_to_review}}.

Prioritize, in order: bugs and data loss, security, user isolation (UserId filtering), validation, migration safety, the third-party license policy (AGENTS.md), then drift from the design system:
- color literals or new aliases instead of tokens; page CSS that repeats components.css; Kendo or Tabulator styled outside vendor-sync.css;
- a page missing from ViewModels/AppNavigation.cs, or a second page title in the content;
- a keydown listener instead of Toolbox.keys, window.confirm or a hand-made dialog instead of Toolbox.confirm, echarts.init instead of charts.js;
- hard-coded or mixed-language text, dates or numbers not formatted for the UI culture;
- a list or data area without empty, loading or error state; a delete or external write without a confirmation naming the item;
- no tests/ui update, or no line in docs/UI_UX_REVIEW.md for a screen change.

Respond with:
1. Findings with file and line, most severe first
2. Remaining risks or test gaps (say so explicitly if there are no findings)
```

## Maintenance Handoff (seed: MaintenanceHandoff)

Variables: `{{module}}`

```text
[Shared context block from docs/FEATURE_PROMPTS.md]

Summarize module {{module}} for a new maintainer.

Respond with:
1. Purpose
2. Controller and actions, and its entry in ViewModels/AppNavigation.cs
3. Services
4. Domain entities
5. ViewModels and Razor views
6. Shared building blocks it uses (components.css classes, partials, Toolbox helpers, keyboard shortcuts)
7. Front-end assets (CSS, JS, third-party widgets) and their licenses
8. Data flow
9. Risks when changing it
10. Verification checklist
```

## Third-Party License Check (docs only, not seeded yet)

Variables: `{{package_name}}`, `{{package_version}}`, `{{intended_use}}`

```text
[Shared context block from docs/FEATURE_PROMPTS.md]

I want to add or upgrade {{package_name}} version {{package_version}} for: {{intended_use}}.

Using the license policy in AGENTS.md (allowed: MIT, Apache-2.0, BSD, ISC, OFL for fonts; owner approval: MPL, LGPL, free proprietary vendor licenses, license files without SPDX; forbidden: commercial, trial, GPL/AGPL for shipped code, pre-release terms outside development):

Respond with:
1. The package's actual license, quoted from its LICENSE file
2. Its runtime dependencies and their licenses (flag any that are commercial, like @progress/kendo-drawing)
3. Verdict: allowed, needs owner approval, or forbidden, with the reason
4. The THIRD-PARTY-NOTICES.md entry to add
5. Allowed alternatives if the verdict is not "allowed"
```
