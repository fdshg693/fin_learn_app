# Claude CLI・SDKを使った自動化スクリプトフォルダ

## 重要な決定

- `.claude\agents` ・ `.claude\skills` などの組み込みのエージェント・スキルには依存しない
    - より柔軟で、独自のエージェント・スキル管理が可能
- **コード修正等の後に、必ず`.claude\rules\claude-automate.md`の内容を 同期させること**

## セットアップ

```bash
cd claude-automate
npm install
```

## 使い方

```bash
node run.mjs [--agent エージェント名] [--prompt プロンプトファイル名] [--prompt-raw "生のプロンプト文字列"]
node run.mjs --workflow ワークフロー名
```

すべてフラグ指定。位置引数は受け付けない（誤指定はエラーになる）。

- プロンプトファイルは `prompts/` ディレクトリに `.md` で配置する
- `--agent` を省略すると `plain` が使われる
- `--prompt` を省略した場合、エージェントの `defaultPrompt` → `default.md` の順で解決される
- `--prompt-raw` を指定するとプロンプトファイルの代わりに引数の文字列がそのままプロンプトとして渡される（`--prompt` より優先）
- `--workflow` 指定時は、`--agent` ・`--prompt` ・`--prompt-raw` の指定は無視される
- 実行終了後、各ステップの結果ログファイルのパスを標準出力に表示する
- ヘルプは `node run.mjs --help` で確認できる

### エージェント一覧

| 名前 | `--agent` 値 | 説明 | 利用ツール |
|------|-------------|------|-----------|
| Plain Agent | `plain` (デフォルト) | ツールなし。テキストのみで回答 | なし |
| File Agent | `file` | ファイルの読み書き・検索が可能 | Read, Edit, Glob, Grep |

### 実行例

```bash
# Plain Agent でシンプルな質問に回答
node run.mjs --prompt default.md

# File Agent でファイルを読んで要約
node run.mjs --agent file --prompt file-test.md

# 生のプロンプト文字列を直接渡す
node run.mjs --prompt-raw "今日の日付を教えて"

# ワークフロー（直列）を実行
node run.mjs --workflow sample
```

> **注意:** File Agent はカレントディレクトリ配下のファイルにアクセスする。
> プロジェクトルートから実行すること。

### エージェントの追加

`agents/` ディレクトリに JSON ファイルを追加するだけで拡張できる。
ファイル名（拡張子除く）が `--agent` の値になる。

```jsonc
// agents/bash.json
{
  "name": "Bash Agent",                   // 省略時はファイル名が表示名になる
  "description": "シェルコマンドを実行できる",
  "defaultPrompt": "bash-task.md",        // デフォルトプロンプト（省略可）
  "defaultPromptRaw": "ls -la を実行して", // 生プロンプト（省略可、defaultPrompt より優先）
  "options": {
    "maxTurns": 5,
    "allowedTools": ["Read", "Bash", "Glob"],
    "permissionMode": "acceptEdits"
  }
}
```

### ワークフローの追加

`workflows/` ディレクトリに JSON ファイルを追加する。
ファイル名（拡張子除く）が `--workflow` の値になる。

```jsonc
// workflows/sample.json
{
  "name": "Sample Workflow",        // 省略時はファイル名
  "description": "...",             // 省略可
  "steps": [
    { "agent": "file",  "prompt": "step1.md" },
    { "agent": "plain", "prompt": "step2.md" },        // prompt は省略可
    { "agent": "plain", "promptRaw": "あいさつして" }    // promptRaw は prompt より優先
  ]
}
```

- 各ステップは指定されたエージェントとプロンプトで順番に実行される
- `promptRaw` を指定すると文字列がそのままプロンプトとして渡される（`prompt` より優先）
- `prompt` ・`promptRaw` をいずれも省略するとエージェントの `defaultPromptRaw` → `defaultPrompt` → `default.md` の順で解決される
- 現状は直列実行のみサポート