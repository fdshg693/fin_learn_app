# STEP5: コントローラ/エンドポイント実装

## 目的
STEP3で設計したAPIをMVCのコントローラとして実装し、フロントが参照できるエンドポイントを用意する。

## 実装内容
- TickersController
  - GET /api/tickers
  - GET /api/tickers/{tickerId}
- PortfoliosController
  - GET /api/portfolios/{investorId}
- ActionsController
  - POST /api/actions
  - いったんシミュレーションは未実装で、現在は変更無しの結果を返す

## 追加ファイル
- backend/FinLearnApp.Api/Controllers/TickersController.cs
- backend/FinLearnApp.Api/Controllers/PortfoliosController.cs
- backend/FinLearnApp.Api/Controllers/ActionsController.cs

## 補足
- STEP8でアクションの実処理を追加する。
- 価格・評価額はインメモリのシードデータを参照する。
