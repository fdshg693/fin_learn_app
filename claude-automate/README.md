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
node run.mjs [プロンプトファイル名] [--agent エージェント名]
node run.mjs --workflow ワークフロー名
```

- プロンプトファイルは `prompts/` ディレクトリに `.md` で配置する
- `--agent` を省略すると `plain` が使われる
- プロンプトファイルを省略した場合、エージェントの `defaultPrompt` → `default.md` の順で解決される
- `--workflow` 指定時は、`--agent` ・プロンプト引数の指定は無視される
- 実行終了後、各ステップの結果ログファイルのパスを標準出力に表示する

### エージェント一覧

| 名前 | `--agent` 値 | 説明 | 利用ツール |
|------|-------------|------|-----------|
| Plain Agent | `plain` (デフォルト) | ツールなし。テキストのみで回答 | なし |
| File Agent | `file` | ファイルの読み書き・検索が可能 | Read, Edit, Glob, Grep |

### 実行例

```bash
# Plain Agent でシンプルな質問に回答
node run.mjs default.md

# File Agent でファイルを読んで要約
node run.mjs file-test.md --agent file

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
  "name": "Bash Agent",          // 省略時はファイル名が表示名になる
  "description": "シェルコマンドを実行できる",
  "defaultPrompt": "bash-task.md", // デフォルトプロンプト（省略可）
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
    { "agent": "plain", "prompt": "step2.md" }  // prompt は省略可
  ]
}
```

- 各ステップは指定されたエージェントとプロンプトで順番に実行される
- `prompt` を省略するとエージェントの `defaultPrompt` → `default.md` の順で解決される
- 現状は直列実行のみサポート

## プロジェクト構造

```
claude-automate/
├── run.mjs              # エントリポイント
├── lib/
│   ├── cli.mjs          # CLI 引数パース
│   ├── agents.mjs       # agents/ からエージェント定義を読み込み
│   ├── prompts.mjs      # prompts/ からプロンプトを読み込み
│   ├── workflows.mjs    # workflows/ からワークフロー定義を読み込み
│   └── runner.mjs       # SDK 実行 & ストリーミング出力（結果ログパスを返す）
├── agents/              # エージェント定義（1ファイル = 1エージェント）
│   ├── plain.json
│   └── file.json
├── prompts/             # プロンプトテンプレート
│   ├── default.md
│   └── file-test.md
└── workflows/           # ワークフロー定義（1ファイル = 1ワークフロー）
    └── sample.json
```
