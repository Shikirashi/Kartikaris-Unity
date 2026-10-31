# Kartikaris Repository Instructions

## Persistent documentation workflow

Before an implementation task, read `Docs/PROJECT_STATE.md`. Use it for current systems, scene setup, architecture decisions, limitations, milestone, and pending work, but verify relevant claims against the repository. The repository is authoritative when code and documentation disagree.

Whenever a task meaningfully changes implementation, configuration, scenes, assets, architecture, packages, editor tooling, or other repository state:

1. Update only the affected sections of `Docs/PROJECT_STATE.md` so it describes the resulting current state. It is a concise reference, not a chronological diary.
2. Create one uniquely named report in `Docs/ImplementationReports/`, using `YYYY-MM-DD_<descriptive-task-name>.md`.

Implementation includes changes to C# code, Unity scenes or prefabs, ScriptableObjects, shaders/materials, project or input configuration, packages, gameplay, procedural generation, rendering, editor tooling, and Kartikaris-specific asset setup. Read-only analysis, planning, questions, and searches do not require a report. Documentation-only maintenance and small documentation typo fixes do not recursively require reports.

Each implementation report must distinguish implemented behavior, observed existing behavior, and proposed future work. Use these sections where applicable:

- `# <Task Name>`
- `## Summary`
- `## Files Created`
- `## Files Modified`
- `## Scene / Prefab / Inspector Changes`
- `## Implementation Details`
- `## Decisions and Rationale`
- `## Validation Performed`
- `## Known Issues / Limitations`
- `## Manual Follow-up`
- `## Suggested Next Step`

Write `None.` or `None known.` where appropriate. Record actual validation only; never imply Unity, runtime, or tests were run when they were not. Historical reports should remain unchanged when later work supersedes them unless correcting a factual documentation error.

At the end of every implementation task:

1. Inspect the resulting diff and status.
2. Account for every intentional change and check for accidental unrelated changes.
3. Perform appropriate available validation and disclose failures or unavailable checks.
4. Update `Docs/PROJECT_STATE.md`.
5. Add the implementation report.
6. Mention both documentation updates in the final response.

Preserve the capitalization of existing paths, especially `Docs/`. Do not treat Ritual Division reference material as Kartikaris' current architecture.
