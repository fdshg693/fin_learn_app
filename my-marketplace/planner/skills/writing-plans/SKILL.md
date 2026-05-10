---
# スペックを元に実装プランを作るスキル。
name: writing-plans
# 複数ファイルに分けた実装プランを作れるため、後続でタスク分割しやすい
# スペックから実装プランを作るので、所要時間はスペックの内容次第。曖昧・複雑なスペックほど時間がかかる。
# セルフレビュー＋サブエージェントレビューを行うオーバーヘッドあり
description: Use when you have a spec or requirements for a multi-step task, before touching code
# 前段スキル: brainstorming (必須ではなく、spec等が出来ていればOK)
# 入力ファイル: docs/specs/<topic>-design.md (必須ではない。brainstorming スキルの後続として使う場合は、brainstorming の出力がこれになるはず)
# 出力ファイル: docs/writing-plans/<feature-name>.md + docs/writing-plans/<feature-name>/*.md (one file per task)
---

# Writing Plans

## Overview

Write comprehensive implementation plans assuming the engineer has zero context for our codebase and questionable taste. Document everything they need to know: which files to touch for each task, code, testing, docs they might need to check, how to test it. Give them the whole plan as bite-sized tasks. DRY. YAGNI. TDD. Frequent commits.

Assume they are a skilled developer, but know almost nothing about our toolset or problem domain. Assume they don't know good test design very well.

## File Layout

Plans are split across multiple files: a thin entry-point file plus one file per task.

- **Entry point:** `docs/writing-plans/<feature-name>.md`
  - Thin overview only: header (Goal / Architecture / Tech Stack), File Structure section, and a table of contents linking to each task file. No task bodies inline.
- **Task files:** `docs/writing-plans/<feature-name>/<task-slug>.md`
  - One file per task. Contains the full Task block (Files / Steps / code / commands / commit).
  - `<task-slug>` is a kebab-case short name describing the task (e.g. `trade-result-logging.md`, `order-book-matching.md`). Prefix with the task number when ordering matters: `01-trade-result-logging.md`.

Example:

```
docs/writing-plans/
  trade-history.md                     ← entry point (overview + TOC)
  trade-history/
    01-trade-result-logging.md         ← Task 1 full content
    02-history-view-component.md       ← Task 2 full content
    03-persistence-layer.md            ← Task 3 full content
```

The entry point and the task files together are the plan. Neither stands alone.

## Scope Check

If the spec covers multiple independent subsystems, it should be broken into sub-project specs. 
— one per subsystem. Each plan should produce working, testable software on its own.

## File Structure

Before defining tasks, map out which files will be created or modified and what each one is responsible for. This is where decomposition decisions get locked in.

- Design units with clear boundaries and well-defined interfaces. Each file should have one clear responsibility.
- You reason best about code you can hold in context at once, and your edits are more reliable when files are focused. Prefer smaller, focused files over large ones that do too much.
- Files that change together should live together. Split by responsibility, not by technical layer.
- In existing codebases, follow established patterns. If the codebase uses large files, don't unilaterally restructure - but if a file you're modifying has grown unwieldy, including a split in the plan is reasonable.

This structure informs the task decomposition. Each task should produce self-contained changes that make sense independently.

## Bite-Sized Task Granularity

**Each step is one action (2-5 minutes):**
- "Write the failing test" - step
- "Run it to make sure it fails" - step
- "Implement the minimal code to make the test pass" - step
- "Run the tests and make sure they pass" - step
- "Commit" - step

## Entry Point File Structure

The entry point file (`docs/writing-plans/<feature-name>.md`) is a thin overview. It MUST contain only:

1. **Header** (required):

```markdown
# [Feature Name] Implementation Plan

**Goal:** [One sentence describing what this builds]

**Architecture:** [2-3 sentences about approach]

**Tech Stack:** [Key technologies/libraries]

---
```

2. **File Structure section** — the cross-task map of files to create/modify (see "File Structure" above).

3. **Tasks (Table of Contents)** — a numbered list linking to each task file. One line per task, with a one-sentence summary:

```markdown
## Tasks

1. [Trade Result Logging](trade-history/01-trade-result-logging.md) — Capture buy/sell results into a structured log entry.
2. [History View Component](trade-history/02-history-view-component.md) — Render the log as a sortable table in the React UI.
3. [Persistence Layer](trade-history/03-persistence-layer.md) — Save/load history via the existing repository interface.
```

**Do NOT inline task bodies, code blocks, or step lists in the entry point.** Those live in the task files.

## Task File Structure

Each task file (`docs/writing-plans/<feature-name>/<task-slug>.md`) contains the full task block. Use this template:

````markdown
# Task N: [Component Name]

[← Back to plan](../<feature-name>.md)

**Files:**
- Create: `exact/path/to/file.py`
- Modify: `exact/path/to/existing.py:123-145`
- Test: `tests/exact/path/to/test.py`

- [ ] **Step 1: Write the failing test**

```python
def test_specific_behavior():
    result = function(input)
    assert result == expected
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pytest tests/path/test.py::test_name -v`
Expected: FAIL with "function not defined"

- [ ] **Step 3: Write minimal implementation**

```python
def function(input):
    return expected
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pytest tests/path/test.py::test_name -v`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add tests/path/test.py src/path/file.py
git commit -m "feat: add specific feature"
```
````

## No Placeholders

Every step must contain the actual content an engineer needs. These are **plan failures** — never write them:
- "TBD", "TODO", "implement later", "fill in details"
- "Add appropriate error handling" / "add validation" / "handle edge cases"
- "Write tests for the above" (without actual test code)
- "Similar to Task N" (repeat the code — the engineer may be reading tasks out of order)
- Steps that describe what to do without showing how (code blocks required for code steps)
- References to types, functions, or methods not defined in any task

## Remember
- Exact file paths always
- Complete code in every step — if a step changes code, show the code
- Exact commands with expected output
- DRY, YAGNI, TDD, frequent commits
- Entry point stays thin: header + File Structure + TOC. Task bodies live in their own files.
- Every TOC link in the entry point must resolve to an existing task file, and every task file must be linked from the TOC.

## Review

### 1.Self-Review

After writing the complete plan, look at the spec with fresh eyes and check the plan against it. This is a checklist you run yourself — not a subagent dispatch.

**1. Spec coverage:** Skim each section/requirement in the spec. Can you point to a task that implements it? List any gaps.

**2. Placeholder scan:** Search your plan for red flags — any of the patterns from the "No Placeholders" section above. Fix them.

**3. Type consistency:** Do the types, method signatures, and property names you used in later tasks match what you defined in earlier tasks? A function called `clearLayers()` in Task 3 but `clearFullLayers()` in Task 7 is a bug. Open each task file and cross-check — names defined in one file must match the names referenced from another.

**4. Link integrity:** Every entry-point TOC link points to a task file that exists. Every task file is reachable from the TOC. Each task file's "Back to plan" link resolves.

If you find issues, fix them inline. No need to re-review — just fix and move on. If you find a spec requirement with no task, add the task.

### 2. Subagent-Review

Use this template when dispatching a plan document reviewer subagent.

**Purpose:** Verify the plan is complete, matches the spec, and has proper task decomposition.

**Dispatch after:** The complete plan is written.

#### prompt for review subagent:
```markdown
You are a plan document reviewer. Verify this plan is complete and ready for implementation.

**Plan entry point:** [ENTRY_POINT_FILE_PATH] (e.g. docs/writing-plans/<feature-name>.md)
**Task files directory:** [TASK_DIR_PATH] (e.g. docs/writing-plans/<feature-name>/)
**Spec for reference:** [SPEC_FILE_PATH] or [TEXT_OF_SPEC]

Read the entry point first to get the TOC, then read every linked task file.
The plan is the entry point + all task files together.

## What to Check

| Category | What to Look For |
|----------|------------------|
| Completeness | TODOs, placeholders, incomplete tasks, missing steps |
| Spec Alignment | Plan covers spec requirements, no major scope creep |
| Task Decomposition | Tasks have clear boundaries, steps are actionable |
| Buildability | Could an engineer follow this plan without getting stuck? |
| File Layout | Entry point is thin (no inlined task bodies). Every TOC link resolves. Every task file is linked from the TOC. Names used across task files are consistent. |

## Calibration

**Only flag issues that would cause real problems during implementation.**
An implementer building the wrong thing or getting stuck is an issue.
Minor wording, stylistic preferences, and "nice to have" suggestions are not.

Approve unless there are serious gaps — missing requirements from the spec,
contradictory steps, placeholder content, or tasks so vague they can't be acted on.

## Output Format

## Plan Review

**Status:** Approved | Issues Found

**Issues (if any):**
- [Task X, Step Y]: [specific issue] - [why it matters for implementation]

**Recommendations (advisory, do not block approval):**
- [suggestions for improvement]
```

**Reviewer returns:** Status, Issues (if any), Recommendations

Loop until status is "Approved". 
If issues are found, fix them and re-dispatch the review.
If recommendations are given, always fix them, but they do not need to be re-reviewed.