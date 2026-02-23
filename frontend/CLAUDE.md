## Frontend

React 19 + React Router v7（フレームワークモード、SSR 有効）+ Tailwind CSS v4 のアプリケーション。
バックエンド（.NET Minimal API、デフォルト: `http://localhost:5088`）とは CORS で接続。

### Commands

```shell
npm run dev        # Vite dev server (localhost:5173)
npm run build      # Production build
npm run typecheck  # TypeScript type check + route type generation
```

### Architecture

- React Router v7 のファイルベースルーティング（`app/routes/`）
- データ取得は `clientLoader`、アクションは `clientAction` で API を呼び出す
- ゲーム状態はサーバー駆動（`GameResponse` をそのまま描画、クライアント側の派生状態なし）
- 環境変数 `VITE_API_URL` で API ベース URL を設定可能

### Key Files

- `app/routes.ts` — ルート定義
- `app/root.tsx` — 共通レイアウト・ErrorBoundary
- `react-router.config.ts` — SSR 有効（デフォルト）

### Screen Design

画面設計の詳細は @./../docs/FRONT.md を参照。
