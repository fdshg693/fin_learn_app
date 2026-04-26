---
paths:
  - "claude-automate/**"
---

## Overview

Claude Agent SDK (`@anthropic-ai/claude-agent-sdk`) を使った自動化ランナー。`.claude/agents` 等の組み込み機構には依存せず、独自にエージェントとプロンプトを管理する。

## Architecture

```
run.mjs  (エントリポイント: 引数パース → ステップ列を構築 → 直列実行 → 結果サマリー出力)
lib/
  cli.mjs        CLI 引数パース (parseArgs) — --workflow / --agent / 位置引数
  agents.mjs     agents/ ディレクトリから JSON を読み込み (loadAgents)
  prompts.mjs    prompts/ から読み込み (loadPrompt) + 名前解決 (resolvePromptName)
  workflows.mjs  workflows/ ディレクトリから JSON を読み込み (loadWorkflow)
  runner.mjs     SDK の query() を呼びストリーミング出力 (run) — { logPath } を返却
```

データフロー: `CLI args → parseArgs → (workflow指定 ? loadWorkflow : 単発) → ステップ列 → 各ステップで loadAgents + loadPrompt + run → 結果ログパスのサマリー`

単発実行とワークフロー実行は **「ステップ列の直列実行」** に統一されている（DRY）。単発は要素1個のステップ列として扱う。

## Key Conventions

- **エージェント定義は `agents/*.json`** — 1ファイル1エージェント。ファイル名（拡張子除く）が `--agent` の値。JSON 内 `name` フィールドで表示名を上書き可能
  > Pattern exemplar: `agents/file.json`
- **プロンプトは `prompts/*.md`** — Markdown テキスト。CLI の位置引数で指定
- **ワークフロー定義は `workflows/*.json`** — 1ファイル1ワークフロー。ファイル名（拡張子除く）が `--workflow` の値。`steps` 配列に `{ agent, prompt? }` を列挙。現状は直列実行のみ
  > Pattern exemplar: `workflows/sample.json`
- **`--workflow` 指定時は `--agent` ・プロンプト引数は無視される**
- **`run()` は結果ログパス `{ logPath }` を返す** — エントリポイントが各ステップのログパスを集約して標準出力にサマリー出力する
- **ESM (`"type": "module"`)** — すべて `.mjs`、`import/export` を使用
- **SDK の `query()` はストリーミング** — `for await` でメッセージを逐次処理。`message.type` が `"assistant"` または `"result"` の2種

## Agent JSON Schema

```jsonc
{
  "name": "表示名（省略時はファイル名）",
  "description": "説明文（省略可）",
  "defaultPrompt": "my-prompt.md",           // デフォルトプロンプトファイル（省略可）
  "options": {
    "maxTurns": 10,                          // SDK に渡すターン数上限
    "tools": [],                             // 空配列 = ツールなし
    "allowedTools": ["Read", "Edit", ...],   // ホワイトリスト
    "permissionMode": "acceptEdits"          // SDK の権限モード
  }
}
```

`options` はそのまま `query({ options })` に渡される。SDK が受け付けるフィールドをそのまま記述できる。

## Workflow JSON Schema

```jsonc
{
  "name": "表示名（省略時はファイル名）",
  "description": "説明文（省略可）",
  "steps": [
    { "agent": "file",  "prompt": "step1.md" },
    { "agent": "plain", "prompt": "step2.md" }   // prompt は省略可
  ]
}
```

各ステップの `prompt` 省略時はエージェントの `defaultPrompt` → `default.md` の順で解決される。

## Prompt Resolution Order

プロンプトファイルの解決優先順位: **明示指定（CLI 引数 or workflow step の `prompt`） > `defaultPrompt` > `default.md`**
共通ヘルパー: `resolvePromptName(promptName, agent)` (`lib/prompts.mjs`)

<!-- Last updated by agent: 2026-04-26 -->
