# simple-mcp-app

MCP Apps SDK (`@modelcontextprotocol/ext-apps`) を使ったインタラクティブ UI アプリのシンプルな例。

LLM がツールを呼ぶと複数の質問 UI が表示され、各質問に対して提示された選択肢ボタンか自由入力で回答でき、ユーザーが全質問を送信した結果をサーバー側ツールで処理する。

## アーキテクチャ

```
Host (Claude Desktop 等)
  │
  │  ① LLM が interactive-select ツールを呼ぶ（questions: [...]）
  │     → UI リソース (view.html) がレンダリングされる
  │
  ▼
┌─────────────────────────────────────┐
│  App (index.html — iframe 内)       │
│                                     │
│  ontoolinput → 質問群を選択肢+入力欄で表示 │
│  click/typing → answer-input を更新  │  ② ユーザーが回答（選択 or 自由入力）
│  submit → callServerTool             │  ③ 全回答をまとめて送信
│  ontoolresult → 結果を表示           │
│  oncalltool → App提供ツール          │
└──────────────┬──────────────────────┘
               │ callServerTool("process-choice", { answers })
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
| `interactive-select` | LLM | 1つ以上の質問をUIで表示し、ユーザーが全回答を送信するまで待機して回答配列を返す。引数: `questions`（`{ choices: string[], prompt?: string }` の配列）, `timeoutMs`（任意, 既定 300000ms — 全回答を待つ共通タイムアウト）。タイムアウト時は `isError: true` で返す |
| `process-choice` | App のみ | ユーザーが入力した回答配列を処理し、サーバー側に保存しつつ `interactive-select` の待機を解放する。引数: `answers: string[]`（質問順、選択肢クリックでも自由入力でも文字列） |
| `get-latest-selection` | LLM | 最新のユーザー回答群（配列と時刻）を取得する。引数なし |

### App ツール（`src/main.ts` — クライアント側）

| ツール名 | 説明 |
|---|---|
| `get-current-selection` | 現在UIの全入力欄の値を質問順に返す |

## フロー

1. **`ontoolinput`** — ツールが呼ばれたとき引数 (`questions`) を受信し、各質問を「選択肢ボタン + 自由入力欄」のブロックとして並べて表示
2. **入力** — ユーザーは各質問に対して、選択肢ボタンをクリックする（入力欄に値が入る）か、入力欄に直接自由回答を入力する。両方の組み合わせも可能（クリック後に編集する等）
3. **送信ボタン** — 全質問の入力欄に値があることを確認した上で `process-choice({ answers })` を呼ぶ。空欄があればエラーを表示してサーバーには送らない
4. **待機** — `interactive-select` ハンドラはサーバー側で Promise を保留にし、`process-choice` の呼び出し（= 送信）または `timeoutMs` の経過まで戻らない。タイムアウトは全質問を回答し終えるまでの共通リミットで、質問単位ではない
5. **`callServerTool`** — サーバーは `latestSelection` を更新し、保留中の `interactive-select` を resolve して回答配列を LLM へ返す
6. **`ontoolresult`** — 元のツールの実行結果を受信し、画面を更新
7. **`oncalltool`** — App 自身が提供するツール (`get-current-selection`) をホスト/LLM から呼び出し可能
8. **`get-latest-selection`** — LLM がいつでも呼び出し、最新の回答群を取得できる（未回答なら「まだ回答が行われていません」を返す）

`interactive-select` が待機中に再度呼ばれた場合は、前の待機を「新しい interactive-select に置き換えられました」エラーで打ち切り、新しい質問群を表示する。

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
