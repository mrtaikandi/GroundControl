# Planning Conventions (Shared)

This file contains conventions shared by `next-planning-task` and `complete-planning-task` skills. Both skills reference this file to avoid duplication.

## Status Vocabulary

Use the repository's live status values in planning files:

- `planned`
- `in-progress`
- `blocked`
- `completed`

Do not switch live files to `done` unless the repository is intentionally normalized later.

## Completion Standard

Only mark a task `completed` when all of the following are true:

- the requested implementation is in place
- the relevant verification passed
- the completed acceptance criteria are supported by evidence
- any remaining gaps are documented instead of hidden

If the work is partially done, leave the task unfinished and document the blocker or remaining work.

## Planning Updates After Verified Completion

### Task File

Update the selected `planning/milestones/.../tasks/T###-*.md` file:

- set frontmatter `status: completed`
- check off implementation checklist items that were actually finished
- check off acceptance criteria and verification steps that were actually validated
- update `Output / Evidence` with verification commands run and their short results
- update `Post-Implementation Notes` with deviations, evidence, or follow-up work when needed

### Milestone File

Update the related `planning/milestones/.../milestone.md` file:

- update the matching row in the `## Tasks` table to `completed`
- if at least one task is complete and the milestone still has unfinished tasks, set milestone frontmatter to `status: in-progress`
- if every task in the milestone is `completed` and milestone-level verification was satisfied, set milestone frontmatter to `status: completed`
- only mark the milestone `completed` after checking the milestone's success criteria, exit gates, and verification matrix

### Planning Index

If the milestone status changes, update the corresponding milestone row in `planning/README.md` so the planning index stays consistent.

## Shared Guardrails

- Never mark a task or milestone complete based on code edits alone.
- Never overwrite unexpected planning changes without reading and reconciling them.
- Do not change `owner`, dates, or scope text unless the work actually requires it or the user asks.
