# STEP6: フロントの画面構成とルーティング

## 目的
フロントエンドで画面の枠組みとルーティングを先に作り、API接続前でも画面遷移が確認できる状態にする。

## 実施内容
- React Routerを導入
- 画面コンポーネントを作成
- ルーティングとナビゲーションを追加

## 追加・更新ファイル
- frontend/src/main.tsx（BrowserRouter追加）
- frontend/src/App.tsx（レイアウト/Routes）
- frontend/src/App.css（レイアウトスタイル）
- frontend/src/pages/Home.tsx
- frontend/src/pages/Tickers.tsx
- frontend/src/pages/TickerDetail.tsx
- frontend/src/pages/Portfolios.tsx
- frontend/src/pages/PortfolioDetail.tsx
- frontend/src/pages/Actions.tsx
- frontend/src/pages/NotFound.tsx

## 補足
- 画面はプレースホルダー。API連携はSTEP7で追加する。
- 仮のリンクIDはダミー。データ連携後に差し替える。
