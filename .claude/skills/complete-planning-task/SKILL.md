---
name: complete-planning-task
description: 'Mark a planning task as completed after verifying requirements, acceptance criteria, and verification steps'
argument-hint: 'A task id like T007 or "current" for the active editor file'
---

# Complete Planning Task

Use this skill to mark a specific planning task as completed after verifying that all of the task's requirements, acceptance criteria, and verification steps are satisfied. This includes updating the task file, related milestone file, and planning index to reflect the new status.

**IMPORTANT**: This skill must not make any code changes, and should only update the planning and milestone files. If any steps are unfinished or unclear, stop and ask the user how they want to proceed, by providing valid options.

## When to Use

- The user asks to complete or finish a specific task such as `T007`.
- The user has a task file open and wants the active planning task completed.
- The user gives a task file path under `planning/milestones/.../tasks/`.

## Task Selection Rules

1. If the user explicitly names a task file, `T###`, or task path, use that task.
2. If the active editor file is a planning task file and the user refers to the current or active task, use that file.
3. If both an explicit task and an active task file exist, prefer the explicit user request.
4. If no specific task can be identified, stop and ask the user which task to complete instead of auto-selecting one.
5. Prefer the live task file over summaries or copied excerpts if they disagree.

## Required Context

Before taking any action, read:

- the selected task file
- the related milestone file
- `planning/README.md`
- the task's `Inputs` and `Traceability` references

Use an **Explore subagent** if there are many traceability references to check, keeping the main context focused on the completion assessment.

## Execution Workflow

1. State which task and milestone you selected. By writing exactly "Selected task T###: Task Title from Milestone M##: Milestone Title" you confirm that you read the relevant planning documents and are grounding your assessment in them.
2. Confirm you've read all Required Context before proceeding.
3. Write a completion assessment comparing the current implementation state against the task's checklist, acceptance criteria, and verification steps. Do not make code changes as part of this skill.
4. Review the implementation checklist, acceptance criteria, verification steps, and dependencies; if any are unfinished or unclear, stop and ask the user how they want to proceed.
5. If the user confirms the work is complete even if some criteria are unmet, mark the task as completed.
6. If verification fails or remains incomplete, do not mark the task `completed`.

## Conventions

Read and follow the shared conventions in `.claude/skills/_shared/planning-conventions.md` for status vocabulary, completion standard, planning updates, and shared guardrails.

If the selected task completes the final unfinished task in its parent milestone, mark the parent milestone `completed` as well after confirming the milestone-level verification and exit criteria are satisfied.

## Guardrails

- Never make code changes; this skill is planning-only.
- Never skip the task's traceability references before assessing completion.
- Never update planning files without first producing a completion assessment.
- If any steps remain unfinished or unclear, ask the user how to proceed before making edits.
- If the task has unmet dependencies, call that out explicitly and let the user decide whether to proceed at risk.

## After Completion

Suggest the user run `/clear` before picking up the next task to reset context.

## Example Prompts

- `/complete-planning-task current`
- `Complete T007 from the planning docs.`
- `Mark the active planning task as done and update the milestone.`
- `Finish T009 and keep the milestone status in sync.`
