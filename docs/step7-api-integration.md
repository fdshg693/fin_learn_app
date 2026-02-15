# STEP7: API連携と表示

## 目的
フロントエンドからバックエンドAPIへアクセスし、取得したデータを画面に表示する。

## 実施内容
- Vite開発サーバーで `/api` をバックエンドへプロキシ
- Ticker / Portfolio 画面でAPIからデータ取得
- 取得中・エラー表示の追加

## 追加・更新ファイル
- frontend/vite.config.ts（APIプロキシ）
- frontend/src/api/client.ts（fetchヘルパー）
- frontend/src/api/types.ts（DTO型定義）
- frontend/src/pages/Tickers.tsx
- frontend/src/pages/TickerDetail.tsx
- frontend/src/pages/Portfolios.tsx
- frontend/src/pages/PortfolioDetail.tsx

## 補足
- Actions画面の実処理はSTEP8で追加する。
- Portfolio一覧はデモ投資家IDで取得している。
