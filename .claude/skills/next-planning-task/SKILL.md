---
name: next-planning-task
description: 'Pick up the next unfinished task from planning/ or execute a specific planning task or milestone the user mentions. Use when the user asks for the next roadmap task, names a T### or M## item, wants implementation driven by planning docs, or needs task and milestone statuses updated after verification.'
argument-hint: 'Use next, a task id like T002, a milestone id like M01, or a planning path'
---

# Next Planning Task

Use this skill to turn the planning artifacts in this repository into the active implementation workflow.

## When to Use

- The user asks for the next unfinished task.
- The user names a specific task such as `T002`.
- The user names a milestone such as `M01` and wants the next ready task inside it.
- The user wants the agent to follow `planning/` documents and keep planning status in sync with implementation progress.

## Task Selection Rules

1. If the user explicitly names a task file, `T###`, milestone, or planning path, use that target.
2. If the user names a milestone but not a task, pick the next ready unfinished task inside that milestone.
3. Otherwise, scan `planning/milestones/` in milestone `roadmap_order`, respecting milestone `depends_on`.
4. Inside the first ready milestone, choose the first task whose frontmatter `status` is not `completed` and whose `depends_on` tasks are all `completed`.
5. Prefer the live task file and milestone file over summary tables if the files disagree.
6. If no task is ready, stop and report the blocking dependency instead of guessing.

## Required Context

Before coding, read:

- `planning/README.md`
- the selected milestone file
- the selected task file
- the task's `Inputs` and `Traceability` references
- any repository instructions relevant to the files being edited

## Execution Workflow

1. State which task and milestone you selected. By writing exactly "Selected task T###: Task Title from Milestone M##: Milestone Title" you confirm that you read the relevant planning documents and are grounding your implementation in them. If the user named a specific target, confirm that you found it and will use it.
2. Read the task's goal, scope, implementation checklist, acceptance criteria, verification steps, and traceability links before editing code.
3. After the task is identified and read, switch into planning mode and write an implementation plan grounded in the selected task before making code changes.
4. Implement only the selected task's scope. Do not quietly pull in unrelated planned work.
5. Run the task's verification steps or the closest concrete equivalent when the task lists them imprecisely.
6. If verification fails or remains incomplete, do not mark the task `completed`.

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
- update `Output / Evidence` with the verification commands and short results
- add concise `Post-Implementation Notes` for deviations or follow-up work when needed

### Milestone File

Update the related `planning/milestones/.../milestone.md` file:

- update the matching row in the `## Tasks` table to `completed`
- if at least one task is complete and the milestone still has unfinished tasks, set milestone frontmatter to `status: in-progress`
- if every task in the milestone is `completed` and milestone-level verification was satisfied, set milestone frontmatter to `status: completed`
- only mark the milestone `completed` after checking the milestone's success criteria, exit gates, and verification matrix

### Planning Index

If the milestone status changes, update the corresponding milestone status row in `planning/README.md` so the planning index stays consistent.

## Guardrails

- Never mark a task or milestone complete based on code edits alone.
- Never skip dependency checks when auto-selecting the next task.
- Never jump straight from task selection to implementation; produce the task plan first once the selected planning documents have been read.
- Never overwrite unexpected planning changes without reading and reconciling them.
- Do not change `owner`, dates, or scope text unless the work actually requires it or the user asks.
- If the user asks for a later task out of order, follow the request but call out unmet dependencies and risk.

## Example Prompts

- `/next-planning-task next`
- `Pick the next unfinished planning task and implement it.`
- `Work on T002 and update the milestone once the task is verified.`
- `Continue M01 with the next ready task.`
- `Implement T005 from planning and keep task and milestone status in sync.`