---
# 新プロジェクト開始時などに、Claudeコンテキスト（Ruleファイル）を効率的に構築するためのスキル。
name: init-rules
# 全体を捜査するため、かなりの時間がかかる。既存で使えるドキュメントなどがあれば、一緒に渡して効率を上げる余地がある。
# フォルダ数が多い場合は、サブエージェントに分担させる。
description: Generate `.claude/rules/<topic>.md` rule files that give an AI coding agent the context it cannot efficiently derive from reading individual files. Use sub-agent delegation for large codebases to speed up the process.
# 高コストスキルでかつ、最初くらいしか実行されないため、ユーザーが手動で呼び出す場合だけに限定
disable-model-invocation: true
# 前提スキル: writing-rules
# - RULEファイルの書き方の学習のために利用
# 出力ファイル: `.claude/rules/` 以下のルールファイル群
---

# Generate or Update Rule Files for AI Coding Agents

Analyze this codebase and produce **rule files under `.claude/rules/<topic>.md`** that give an AI coding agent context it cannot efficiently derive from individual files.

## Prerequisite: Follow by invoking `writing-rules` SKILL

All file format, frontmatter spec, scoping rules (narrowest topic scope, ~30 line cap, no `folder/CLAUDE.md`, no root `CLAUDE.md`), topic-naming conventions, and existing examples are defined in the **`writing-rules`** skill. Read and follow it before producing any rule file. This skill only adds the *discovery* and *delegation* strategy on top.

Project-wide notes live in `.claude/rules/architecture.md` with `paths: ["**"]` — never in a root `CLAUDE.md`.

## Execution Strategy

### Sub-Agent Delegation

When the codebase contains more than ~5 top-level directories or multiple distinct services/packages, **you must delegate investigation to sub-agents rather than doing all the work yourself.**

1. **Scout phase** — Quickly scan the repo root (directory listing, README, top-level config files, existing `.claude/rules/`) to identify the major areas and the domain concepts already covered.
2. **Fan out** — Spawn one sub-agent per major area or candidate topic. Each sub-agent receives:
   - The area path or topic it owns
   - A directive to follow the **`writing-rules`** skill for format/scoping
   - The "What to Discover" priorities below
   - A directive to return draft rule file(s) — one per topic — including the intended path
3. **Parallelize aggressively** — Launch all sub-agents concurrently. Do NOT wait for one area to finish before starting the next.
4. **Synthesize** — Once sub-agents return, deduplicate overlapping rule files, write any missing ones.

### When NOT to Delegate

For small or single-package repos (≤5 top-level directories, single build system), do the analysis yourself.

### Sub-Agent Instructions Template

```
Analyze the area at `{path}` (or the topic `{concept}`).
Follow the `writing-rules` skill for file format, frontmatter, scoping, and naming.
Apply the "What to Discover" priorities from the `init-rules` skill.
Return the full file contents (frontmatter + body) for each proposed rule file,
prefixed with the intended path under `.claude/rules/`.
If a topic warrants long-form documentation, also propose a `docs/...` file and
have the rule body point to it.
```

---

## What to Discover

Prioritize knowledge in this order:

1. **Architecture & Mental Model** — Component boundaries, service topology, data flow directions, and the *reasoning* behind structural decisions. Highest-value content because it requires cross-file reading that wastes agent time.
2. **Non-Obvious Workflows** — Build, test, run, and deploy commands that aren't discoverable from `package.json`, `Makefile`, `*.sln`, or equivalent alone (required env vars, seed steps, order-dependent setup).
3. **Project-Specific Conventions** — Naming schemes, file placement rules, error-handling strategies, and patterns that *diverge from ecosystem defaults*. Include a concrete file path as an exemplar for each convention.
4. **Integration & Boundaries** — External service dependencies, API contracts between internal modules, shared-state mechanisms, and authentication flows.

### Exclusions

Do NOT document:
- Generic best practices ("write tests", "handle errors", "use meaningful names")
- Aspirational patterns not yet reflected in the actual code
- Information already expressed in config files, linter rules, or CI manifests that an agent will read anyway
- Dependency lists reproducible from lockfiles

## Merging with Existing Files

If a rule file already exists:
1. Read it fully before making changes.
2. Preserve any section the codebase still supports — delete only content contradicted by current code.
3. Keep the `paths:` list in sync with where the topic actually lives. Add new paths; remove paths that no longer exist.
4. Append or update sections; do not reorder without reason.

If you find a `folder/CLAUDE.md` (other than the root) or content in the root `CLAUDE.md` that should be a rule, migrate it into an appropriately-named rule file under `.claude/rules/` and delete the old content.

## Style Guide

- Use imperative, present-tense prose ("Services communicate via gRPC", not "Services should communicate…").
- Lead each section with the single most important sentence — agents may truncate.
- When describing a pattern, always include at least one concrete file path as exemplar.
- Prefer tables or tight bullet lists over paragraphs.
- Match the language of existing docs in the repo (e.g. Japanese headings if surrounding rule files use them).
