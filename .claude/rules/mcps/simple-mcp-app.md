---
paths:
  - "mcps/simple-mcp-app/**"
---

## Simple MCP App

`mcps/simple-mcp-app/README.md` が正本。MCP Apps SDK (`@modelcontextprotocol/ext-apps`) を使ったインタラクティブ UI ツールの最小例で、「サーバー側ツール」「UI 用 single-file HTML」「App 側スクリプト」の3点セットで成り立つ。

## Architecture

- `mcps/simple-mcp-app/index.ts` — stdio MCP server。`registerAppTool` / `registerAppResource` で UI 付きツールと UI リソースを登録する
- `mcps/simple-mcp-app/index.html` + `mcps/simple-mcp-app/src/main.ts` — Vite で single-file HTML にバンドルされる App 側コード。`new App(...)` を使い `ontoolinput` / `ontoolresult` / `oncalltool` / `onlisttools` の各ハンドラで UI とサーバーを橋渡しする
- `mcps/simple-mcp-app/vite.config.ts` — `vite-plugin-singlefile` で `dist/index.html` 一枚に固める。サーバーは `dist/index.html` を読み込んで `ui://simple-app/view.html` リソースとして配信するので、サーバー起動前に必ず `npm run build:simple-app` を済ませる必要がある
- `dist/` はビルド成果物。手で編集しない

## Key Conventions

- ツール3種の役割分担を崩さない:
  - `interactive-select` — LLM 用。`_meta.ui.resourceUri` で UI を紐付け、引数 `questions: [{ choices, prompt? }]` / `timeoutMs` を受ける（複数質問を一括で出せる）。**ハンドラは Promise で保留にし、`process-choice` が呼ばれるか `timeoutMs` を超えるまで戻らない**。`timeoutMs` は質問単位ではなく全回答を待つ共通リミット。タイムアウトは `isError: true` で返す
  - `process-choice` — App 専用。`_meta.ui.visibility: ["app"]` を必ず付け、LLM から直接呼べないようにする。引数 `answers: string[]`（質問順、選択肢クリックでも自由入力でも文字列）でサーバー側の `latestSelection` を更新し、保留中の `interactive-select` Promise を resolve する
  - `get-latest-selection` — LLM 用。`server.tool(...)` で登録（UI 不要なので `registerAppTool` ではない）。最新の回答配列を返す
- 待機状態は `pending: { resolve, reject, timer }` で 1 件だけ保持する。`interactive-select` が連続して呼ばれたら旧 pending を reject して置き換える（リーク防止のため `clearTimeout` を必ず呼ぶ）
- App 側 UI は質問ごとに「選択肢ボタン群 + 自由入力欄」を並べ、最後の送信ボタン押下時に全回答を `process-choice` へ一括送信する。空欄が残っていればクライアント側で送信を止める
- App 側ツール (`get-current-selection`) は `oncalltool` 内で `params.name` 分岐し、`onlisttools` でも忘れず公開する。複数質問対応後は各入力欄の値を質問順に列挙して返す
- 状態 (`latestSelection` / `pending`) は server プロセスのメモリ上のみ。永続化が必要になったら task-daemon パターンを参考に分離する（このリポジトリでは `mcps/task-daemon` 側の責務）
- スタイルはホストが注入する CSS 変数 (`--color-*`, `--font-*` 等) を最優先で参照し、フォールバック値だけハードコードする
- 起動経路は `mcps/` で `npm run build:simple-app` → `npm run simple-app`（通常は `.mcp.json` から自動起動）。dev は `npm run dev:simple-app` で Vite dev server を立てる
