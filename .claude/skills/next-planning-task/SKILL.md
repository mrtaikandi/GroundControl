---
name: next-planning-task
description: 'Pick up and implement the next planning task, a specific T### task, or M## milestone from planning/'
argument-hint: 'Use next, a task id like T002, a milestone id like M01, or a planning path'
---

# Next Planning Task

Use this skill to turn the planning artifacts in this repository into the active implementation workflow. Each task is implemented in an isolated worktree, delivered as a pull request, code-reviewed by a dedicated reviewer agent, and marked complete after the review cycle.

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

### Phase 1: Task Selection

1. State which task and milestone you selected. By writing exactly "Selected task T###: Task Title from Milestone M##: Milestone Title" you confirm that you read the relevant planning documents and are grounding your implementation in them. If the user named a specific target, confirm that you found it and will use it.
2. Read the task's goal, scope, implementation checklist, acceptance criteria, verification steps, and traceability links before proceeding.

### Phase 2: Worktree Setup

1. Call `EnterWorktree` with name `m##-t###-short-name` (e.g., `m02-t009-scope-crud`) where `short-name` is a kebab-case summary derived from the task title.
2. Rename the branch to use slash-separated format:
   ```
   git branch -m <current-branch> m##/t###-short-name
   ```
   For example: `m02/t009-scope-crud`. Use lowercase, zero-padded numbers matching the actual milestone and task IDs.
3. If `EnterWorktree` fails (e.g., already in a worktree), stop and ask the user how to proceed.

### Phase 3: Implementation

1. **For tasks touching multiple files or with ambiguous scope:** switch into plan mode and write an implementation plan grounded in the selected task before making code changes. **For small, focused tasks** with a clear implementation checklist (single file, well-defined scope): skip plan mode and implement directly.
2. You may delegate implementation to a **foreground subagent** if the task is large or context-intensive. If you delegate:
   - Provide the subagent with the full task description, implementation checklist, acceptance criteria, and relevant file paths.
   - The subagent works in the current worktree directory (it inherits the working directory).
   - When the subagent finishes, it **must** report back a compact summary of: files changed, key decisions made, deviations from the plan, and any open questions.
   - Absorb the summary into the main context before proceeding.
3. Implement only the selected task's scope. Do not quietly pull in unrelated planned work.
4. Run the task's verification steps or the closest concrete equivalent when the task lists them imprecisely.
5. If verification fails, fix the issue before proceeding. Do not move to PR creation with failing verification.

### Phase 4: PR Creation

1. Stage and commit all changes with a conventional commit message following `.github/git-commit-instructions.md`.
2. Push the branch:
   ```
   git push -u origin m##/t###-short-name
   ```
3. Create a pull request:
   ```
   gh pr create --title "<conventional commit title>" --body "$(cat <<'EOF'

   ## Summary
   <bullet points summarizing the changes>

   ## Acceptance Criteria
   <checklist from task file, checked off as appropriate>

   ## Verification
   <verification commands run and their results>

   EOF
   )"
   ```
4. Report the PR URL to the user.

### Phase 5: User Checkpoint

**Pause and wait for the user.** Present:

- The PR URL
- A brief summary of what was implemented and verified
- The prompt: "Review the PR. You can ask questions, request changes, or say **continue** to proceed to code review."

The user may:

- **Ask questions** about the implementation — answer them.
- **Request changes** — make them in the worktree, commit, and push.
- **Say "continue"** or **"review"** — proceed to Phase 6.
- **Say "skip review"** — skip Phase 6 and go directly to Phase 7.

**Do not proceed past this phase without explicit user input.**

### Phase 6: Code Review Loop

1. Spawn a **reviewer agent** following the Reviewer Agent Guidelines below.
2. The reviewer returns a structured verdict (`APPROVE` or `REQUEST_CHANGES`) with comments.
3. **If `REQUEST_CHANGES`:**
   a. Present the review comments to the user for visibility.
   b. Address each actionable comment — fix the code, commit, and push.
   c. Automatically re-spawn the reviewer agent to verify the fixes.
   d. Repeat until the reviewer returns `APPROVE` or **3 review rounds** have been completed.
4. **If 3 rounds complete without `APPROVE`:** stop and ask the user how to proceed.
5. **Once the review passes (`APPROVE`):** present the final status to the user:
   "Code review passed. Say **done** to mark the task complete, or continue iterating."

**Wait for the user to confirm before proceeding to Phase 7.**

### Phase 7: Completion

1. **Exit the worktree:** call `ExitWorktree` with `action: "keep"`. The branch exists on the remote; the local worktree can be cleaned up later.
2. **Update planning files** on the main branch, following the shared conventions in `.claude/skills/_shared/planning-conventions.md`:
   - **Task file:** set `status: completed`, check off completed implementation checklist items and acceptance criteria, update `Output / Evidence` with the PR URL and verification results, note any deviations in `Post-Implementation Notes`.
   - **Milestone file:** update the task row to `completed`. Set milestone to `in-progress` if it was `planned`.
   - **Planning index (`planning/README.md`):** update if the milestone status changed.
3. If this task completes the **final unfinished task** in its parent milestone, check the milestone's success criteria, exit gates, and verification matrix before marking the milestone `completed`.
4. Suggest the user run `/clear` before picking up the next task to reset context.

## Reviewer Agent Guidelines

Spawn the reviewer as a **general-purpose foreground Agent** (no worktree isolation — it only reads). Provide it with:

- The PR number so it can run `gh pr diff <number>` and `gh pr view <number>`
- The task's **acceptance criteria**, **implementation checklist**, and **scope** (copy the relevant sections from the task file into the agent prompt)
- A reminder to check against project conventions in `CLAUDE.md`

Instruct the reviewer to:

1. Read the full PR diff via `gh pr diff`.
2. Evaluate the changes against:
   - **Task scope:** all checklist items addressed, no missing pieces, no unrelated changes
   - **Acceptance criteria:** each criterion satisfied by the diff
   - **Code quality:** naming, structure, error handling, test coverage
   - **Project conventions:** vertical slice architecture, store pattern, DTO conventions, ETag/concurrency flow, validation patterns, etc.
3. Return a structured review:
   - **Verdict:** `APPROVE` or `REQUEST_CHANGES`
   - **Comments:** a numbered list of specific, actionable items. Each comment includes: file path, area/concern, issue description, and suggested fix. Empty list for `APPROVE`.
   - Do **not** flag formatting issues (the auto-formatter handles those).
   - Do **not** flag issues outside the task's scope.

## Conventions

Read and follow the shared conventions in `.claude/skills/_shared/planning-conventions.md` for status vocabulary, completion standard, planning updates, and shared guardrails.

## Guardrails

- Never skip dependency checks when auto-selecting the next task.
- Never jump straight from task selection to implementation without first reading the task's planning documents.
- If the user asks for a later task out of order, follow the request but call out unmet dependencies and risk.
- Never proceed past Phase 5 (user checkpoint) or Phase 6 (review approval) without user confirmation.
- Never update planning files while still in the worktree — always `ExitWorktree` first so updates land on main.
- Never mark a task `completed` if verification failed and was not resolved.
- If `EnterWorktree` fails, stop and ask the user how to proceed.

## Example Prompts

- `/next-planning-task next`
- `Pick the next unfinished planning task and implement it.`
- `Work on T002 and update the milestone once the task is verified.`
- `Continue M01 with the next ready task.`
- `Implement T005 from planning and keep task and milestone status in sync.`