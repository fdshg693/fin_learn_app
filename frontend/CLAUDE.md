## Frontend

React 19 + React Router v7（フレームワークモード、SSR 有効）+ Tailwind CSS v4 のアプリケーション。
バックエンド（.NET Minimal API、デフォルト: `http://localhost:5088`）とは CORS で接続。

### Commands

```shell
npm run dev        # React Router dev server (localhost:5173)
npm run build      # Production build (server + client)
npm run start      # react-router-serve ./build/server/index.js
npm run typecheck  # react-router typegen && tsc
```

### Architecture

- React Router v7 フレームワークモード（SSR 有効、`ssr: true`）
- データ取得は `clientLoader`、アクションは `clientAction` で API を呼び出す（SSR + クライアントサイドフェッチのハイブリッド）
- ゲーム状態はサーバー駆動（`GameResponse` をそのまま描画、クライアント側の派生状態なし）
- 環境変数 `VITE_API_URL` で API ベース URL を設定可能（デフォルト: `http://localhost:5088`）
- Tailwind CSS v4 は `@tailwindcss/vite` プラグイン経由（PostCSS 不要）

### Conventions

- 全コンポーネントは `memo()` でメモ化（不要な再レンダリング防止）
- イベントハンドラは `useCallback()` でメモ化（`TradeForm.tsx`）
- `HydrateFallback()` で SSR ハイドレーション中のローディング UI を表示
- エラーメッセージは日本語（`gameApi.ts` の `ERROR_MESSAGES` マップ）
- ルート型は `react-router typegen` で自動生成（`.react-router/types/`）
- パスエイリアス: `~/` → `./app/`

### Key Files

- `app/routes.ts` — ルート定義
- `app/root.tsx` — 共通レイアウト・ErrorBoundary
- `react-router.config.ts` — SSR モード（`ssr: true`）

### Screen Design

@./../docs/FRONT.md
