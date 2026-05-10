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
| `interactive-select` | LLM | 選択肢をUIで表示し、ユーザーのクリックを待機して選択値を返す。引数: `choices`（配列）, `prompt`（任意）, `timeoutMs`（任意, 既定 300000ms）。タイムアウト時は `isError: true` で返す |
| `process-choice` | App のみ | ユーザーが選んだ値を処理し、サーバー側に保存しつつ `interactive-select` の待機を解放する。引数: `choice` |
| `get-latest-selection` | LLM | 最新のユーザー選択結果（値と時刻）を取得する。引数なし |

### App ツール（`src/main.ts` — クライアント側）

| ツール名 | 説明 |
|---|---|
| `get-current-selection` | 現在UIで選択状態のボタンのテキストを返す |

## フロー

1. **`ontoolinput`** — ツールが呼ばれたとき引数 (`choices`, `prompt`) を受信し、選択肢をボタンとして表示
2. **待機** — `interactive-select` ハンドラはサーバー側で Promise を保留にし、ユーザーのクリック（= `process-choice` の呼び出し）または `timeoutMs` の経過まで戻らない
3. **`callServerTool`** — ユーザーがボタンをクリックすると `process-choice` を呼び出す。サーバーは `latestSelection` を更新し、保留中の `interactive-select` を resolve して選択値を LLM へ返す
4. **`ontoolresult`** — 元のツールの実行結果を受信し、画面を更新
5. **`oncalltool`** — App 自身が提供するツール (`get-current-selection`) をホスト/LLM から呼び出し可能
6. **`get-latest-selection`** — LLM がいつでも呼び出し、最新の選択結果を取得できる（未選択なら「まだ選択が行われていません」を返す）

`interactive-select` が待機中に再度呼ばれた場合は、前の待機を「新しい interactive-select に置き換えられました」エラーで打ち切り、新しい選択肢を表示する。

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
