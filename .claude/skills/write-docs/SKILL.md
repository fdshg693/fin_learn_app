---
name: write-docs
description: always use this SKILL whenever you write documents.
---


# General guidelines for writing documentation in this codebase
Focus on discovering the essential knowledge that would help an AI agents be immediately productive in this codebase. Consider aspects like:

- The "big picture" architecture that requires reading multiple files to understand - major components, service boundaries, data flows, and the "why" behind structural decisions
- Critical developer workflows (builds, tests, debugging) especially commands that aren't obvious from file inspection alone
- Project-specific conventions and patterns that differ from common practices
- Integration points, external dependencies, and cross-component communication patterns

- Write concise, actionable instructions (~20-50 lines) using markdown structure
- Include specific examples from the codebase when describing patterns
- Avoid generic advice ("write tests", "handle errors") - focus on THIS project's specific approaches
- Document only discoverable patterns, not aspirational practices
- Reference key files/directories that exemplify important patterns

# How to write `CLAUDE.md` files

Whenever you find big folders which contain no `CLAUDE.md`, always add `CLAUDE.md` without asking user.

Place `CLAUDE.md` at the narrowest possible scope.
Content specific to `backend` should be in `backend/CLAUDE.md`, not in the root `CLAUDE.md`.
A `folder/CLAUDE.md` should only contain content not already covered by `CLAUDE.md` files in its subfolders, or brief summaries of each subfolder (so it's easy to determine what information exists where).
Also, keep each `CLAUDE.md` to around 150 lines. If splitting into subfolders is not possible or not appropriate, create dedicated content files under `ai/docs` and import them from `CLAUDE.md`.

In `src/CLAUDE.md`, you can import the contents of `ai/docs/feature1/overview.md` by writing @./../ai/docs/feature1/overview.md (omit backticks).