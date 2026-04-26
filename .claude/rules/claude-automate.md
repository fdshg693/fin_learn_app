---
paths:
  - "claude-automate/**"
---

## Overview

Claude Agent SDK (`@anthropic-ai/claude-agent-sdk`) を使った自動化ランナー。`.claude/agents` 等の組み込み機構には依存せず、独自にエージェントとプロンプトを管理する。

## Architecture

```
run.mjs  (エントリポイント: 引数パース → エージェント読み込み → プロンプト読み込み → 実行)
lib/
  cli.mjs      CLI 引数パース (parseArgs)
  agents.mjs   agents/ ディレクトリから JSON を読み込み (loadAgents)
  prompts.mjs  prompts/ ディレクトリからプロンプトを読み込み (loadPrompt)
  runner.mjs   SDK の query() を呼びストリーミング出力 (run)
```

データフロー: `CLI args → parseArgs → loadAgents + loadPrompt → run(agent, prompt) → stdout`

## Key Conventions

- **エージェント定義は `agents/*.json`** — 1ファイル1エージェント。ファイル名（拡張子除く）が `--agent` の値。JSON 内 `name` フィールドで表示名を上書き可能
  > Pattern exemplar: `agents/file.json`
- **プロンプトは `prompts/*.md`** — Markdown テキスト。CLI の位置引数で指定
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

## Prompt Resolution Order

プロンプトファイルの解決優先順位: **CLI 引数 > `defaultPrompt` > `default.md`**

<!-- Last updated by agent: 2026-03-29 -->
