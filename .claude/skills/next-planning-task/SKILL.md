---
name: next-planning-task
description: 'Pick up and implement the next planning task, a specific T### task, or M## milestone from planning/'
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

Use an **Explore subagent** to scan `planning/milestones/` and identify the next ready task. The subagent should read `planning/README.md`, milestone files, and task frontmatter to determine status and dependencies, then report back the selected task file path and a summary.

Once the subagent identifies the task, read in the main context only:

- the selected task file
- the task's `Inputs` and `Traceability` references (use a subagent for bulk reads if there are many)
- any repository instructions relevant to the files being edited

This keeps the main context clean for implementation.

## Execution Workflow

1. State which task and milestone you selected. By writing exactly "Selected task T###: Task Title from Milestone M##: Milestone Title" you confirm that you read the relevant planning documents and are grounding your implementation in them. If the user named a specific target, confirm that you found it and will use it.
2. Read the task's goal, scope, implementation checklist, acceptance criteria, verification steps, and traceability links before editing code.
3. **For tasks touching multiple files or with ambiguous scope:** switch into plan mode and write an implementation plan grounded in the selected task before making code changes. **For small, focused tasks** with a clear implementation checklist (single file, well-defined scope): skip plan mode and implement directly.
4. Implement only the selected task's scope. Do not quietly pull in unrelated planned work.
5. Run the task's verification steps or the closest concrete equivalent when the task lists them imprecisely.
6. If verification fails or remains incomplete, do not mark the task `completed`.

## Conventions

Read and follow the shared conventions in `.claude/skills/_shared/planning-conventions.md` for status vocabulary, completion standard, planning updates, and shared guardrails.

If the selected task completes the final unfinished task in its parent milestone, mark the parent milestone `completed` as well after confirming the milestone-level verification and exit criteria are satisfied.

## Guardrails

- Never skip dependency checks when auto-selecting the next task.
- Never jump straight from task selection to implementation without first reading the task's planning documents.
- If the user asks for a later task out of order, follow the request but call out unmet dependencies and risk.

## Example Prompts

- `/next-planning-task next`
- `Pick the next unfinished planning task and implement it.`
- `Work on T002 and update the milestone once the task is verified.`
- `Continue M01 with the next ready task.`
- `Implement T005 from planning and keep task and milestone status in sync.`