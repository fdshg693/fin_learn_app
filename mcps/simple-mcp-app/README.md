# simple-mcp-app

MCP Apps SDK (`@modelcontextprotocol/ext-apps`) を使ったインタラクティブ UI アプリのシンプルな例。

LLM がツールを呼ぶと選択肢 UI が表示され、ユーザーの選択結果をサーバー側ツールで処理する。

## アーキテクチャ

```
Host (Claude Desktop 等)
  │
  │  ① LLM が interactive-select ツールを呼ぶ
  │     → UI リソース (view.html) がレンダリングされる
  │
  ▼
┌─────────────────────────────────────┐
│  App (index.html — iframe 内)       │
│                                     │
│  ontoolinput → 選択肢をボタン表示   │
│  click → callServerTool             │  ② ユーザーが選択
│  ontoolresult → 結果を表示          │  ③ サーバーツールで処理
│  oncalltool → App提供ツール         │
└──────────────┬──────────────────────┘
               │ callServerTool("process-choice", { choice })
               ▼
┌─────────────────────────────────────┐
│  MCP Server (index.ts — stdio)      │
│                                     │
│  interactive-select  — UI付きツール  │
│  process-choice      — App専用ツール │
│  get-latest-selection — LLM用ツール  │
└─────────────────────────────────────┘
```

## ツール一覧

### サーバーツール（`index.ts`）

| ツール名 | 呼び出し元 | 説明 |
|---|---|---|
| `interactive-select` | LLM | 選択肢をUIで表示しユーザーに選ばせる。引数: `choices`（配列）, `prompt`（任意） |
| `process-choice` | App のみ | ユーザーが選んだ値を処理し、サーバー側に保存する。引数: `choice` |
| `get-latest-selection` | LLM | 最新のユーザー選択結果（値と時刻）を取得する。引数なし |

### App ツール（`src/main.ts` — クライアント側）

| ツール名 | 説明 |
|---|---|
| `get-current-selection` | 現在UIで選択状態のボタンのテキストを返す |

## フロー

1. **`ontoolinput`** — ツールが呼ばれたとき引数 (`choices`, `prompt`) を受信し、選択肢をボタンとして表示
2. **`callServerTool`** — ユーザーがボタンをクリックすると `process-choice` ツールを呼び出し、サーバー側に選択結果を保存
3. **`ontoolresult`** — 元のツールの実行結果を受信し、画面を更新
4. **`oncalltool`** — App 自身が提供するツール (`get-current-selection`) をホスト/LLM から呼び出し可能
5. **`get-latest-selection`** — LLM がいつでも呼び出し、最新の選択結果を取得できる（未選択なら「まだ選択が行われていません」を返す）

## セットアップ

```bash
# mcps/ ディレクトリで
npm install

# App UI をビルド（single-file HTML）
npm run build:simple-app

# サーバー起動（通常は .mcp.json 経由で自動起動）
npm run simple-app
```

## 開発

```bash
# Vite dev server（UIの開発用）
npm run dev:simple-app
```
