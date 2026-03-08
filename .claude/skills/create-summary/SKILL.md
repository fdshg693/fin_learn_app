---
name: create-summary
description: Generate `CLAUDE.md` files that give an AI coding agent the context it cannot efficiently derive from reading individual files. Use sub-agent delegation for large codebases to speed up the process.
---

# Generate or Update `CLAUDE.md` for AI Coding Agents

Analyze this codebase and produce `CLAUDE.md` files that give an AI coding agent the context it cannot efficiently derive from reading individual files.

## Execution Strategy

### Sub-Agent Delegation

When the codebase contains more than ~5 top-level directories or multiple distinct services/packages, **you must delegate investigation to sub-agents rather than doing all the work yourself.**

1. **Scout phase** — Quickly scan the repo root (directory listing, README, top-level config files) to identify the major areas (e.g., `frontend/`, `backend/`, `infra/`, `libs/`, `services/*`).
2. **Fan out** — Spawn one sub-agent per major area. Each sub-agent receives:
   - The area path it owns (e.g., `backend/`)
   - This full instruction set (so it follows the same scoping/style rules)
   - A directive to produce a draft `CLAUDE.md` for its area and return it
3. **Parallelize aggressively** — Launch all sub-agents concurrently. Do NOT wait for one area to finish before starting the next. The whole point of delegation is wall-clock speedup.
4. **Synthesize** — Once all sub-agents return, review their drafts for cross-cutting concerns (shared libraries, inter-service contracts, global conventions). Write or update the **root `CLAUDE.md`** yourself, covering only repo-wide architecture and pointers to child files. Do not duplicate child content.

### When NOT to Delegate

For small or single-package repos (≤5 top-level directories, single build system), do the analysis yourself — the overhead of sub-agent coordination would exceed the time saved.

### Sub-Agent Instructions Template

When spawning a sub-agent, pass a prompt structured like:
```
Analyze the directory at `{path}`. Produce a `CLAUDE.md` (≤150 lines) following these rules:
- [include the "What to Discover" and "How to Write" sections below verbatim or by reference]
- Return ONLY the markdown content of the CLAUDE.md file.
- If you find sub-directories large enough to warrant their own CLAUDE.md (≥3 source files or own build config), note them in your output so the orchestrator can spawn further sub-agents.
```

If a sub-agent reports nested areas needing their own files, spawn a second wave of sub-agents for those — again in parallel.

---

## What to Discover

Prioritize knowledge in this order:

1. **Architecture & Mental Model** — Component boundaries, service topology, data flow directions, and the *reasoning* behind structural decisions. This is the highest-value content because it requires cross-file reading that wastes agent time.
2. **Non-Obvious Workflows** — Build, test, run, and deploy commands that aren't discoverable from `package.json`, `Makefile`, or equivalent alone (e.g., required env vars, seed steps, order-dependent setup).
3. **Project-Specific Conventions** — Naming schemes, file placement rules, error-handling strategies, and patterns that *diverge from ecosystem defaults*. Include a concrete file path as an exemplar for each convention.
4. **Integration & Boundaries** — External service dependencies, API contracts between internal modules, shared-state mechanisms, and authentication flows.

### Exclusions

Do NOT document:
- Generic best practices ("write tests", "handle errors", "use meaningful names")
- Aspirational patterns not yet reflected in the actual code
- Information already expressed in config files, linter rules, or CI manifests that an agent will read anyway
- Dependency lists reproducible from lockfiles

## How to Write `CLAUDE.md`

### Scoping Rules

| Rule | Detail |
|---|---|
| **Narrowest scope** | Content about `backend/` belongs in `backend/CLAUDE.md`, not root. |
| **No duplication** | A parent `CLAUDE.md` must not repeat what a child already covers. It may include a one-line summary pointing to the child. |
| **Size cap** | Target ~50 lines per file; hard max 150 lines. If a single scope exceeds this, split into dedicated docs under a `docs/` or `ai/docs/` directory and import them (see Imports below). |
| **Auto-create** | When you encounter a significant directory (containing ≥3 source files or its own build config) that lacks a `CLAUDE.md`, create one without prompting the user. |

### Imports

From `src/CLAUDE.md`, reference external content with:
```
@./../ai/docs/feature1/overview.md
```

Use imports for deep-dive content (API schemas, migration guides, domain glossaries) that would bloat the host file.

### Merging with Existing Files

If a `CLAUDE.md` already exists:
1. Read it fully before making changes.
2. Preserve any section the codebase still supports — delete only content contradicted by current code.
3. Append or update sections; do not reorder without reason.
4. Leave a blank-line-separated comment `<!-- Last updated by agent: YYYY-MM-DD -->` at the bottom.

### Style Guide

- Use imperative, present-tense prose ("Services communicate via gRPC", not "Services should communicate…").
- Lead each section with the single most important sentence — agents may truncate.
- When describing a pattern, always include at least one concrete file path as exemplar:
  `> Pattern exemplar: src/services/billing/handler.ts`
- Prefer tables or tight bullet lists over paragraphs.
- Omit filler headings; every heading must introduce actionable content.
