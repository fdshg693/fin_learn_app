# Claude CLI・SDKを使った自動化スクリプトフォルダ

## 重要な決定

- `.claude\agents` ・ `.claude\skills` などの組み込みのエージェント・スキルには依存しない
    - より柔軟で、独自のエージェント・スキル管理が可能

## セットアップ

```bash
cd claude-automate
npm install
```

## 使い方

```bash
node run.mjs [プロンプトファイル名] [--agent エージェント名]
```

- プロンプトファイルは `prompts/` ディレクトリに `.md` で配置する
- `--agent` を省略すると `plain` が使われる
- プロンプトファイルを省略した場合、エージェントの `defaultPrompt` → `default.md` の順で解決される

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

## プロジェクト構造

```
claude-automate/
├── run.mjs              # エントリポイント
├── lib/
│   ├── cli.mjs          # CLI 引数パース
│   ├── agents.mjs       # agents/ からエージェント定義を読み込み
│   ├── prompts.mjs      # prompts/ からプロンプトを読み込み
│   └── runner.mjs       # SDK 実行 & ストリーミング出力
├── agents/              # エージェント定義（1ファイル = 1エージェント）
│   ├── plain.json
│   └── file.json
└── prompts/             # プロンプトテンプレート
    ├── default.md
    └── file-test.md
```
