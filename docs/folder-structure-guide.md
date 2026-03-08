# フォルダ構成ガイド（クライアント起点のデータフロー付き）

更新日: 2026-03-08

## このドキュメントの目的
- 「どのフォルダが何を担当するか」を1枚で把握する
- フロントエンドからバックエンドまで、実際のデータの流れを追えるようにする
- 既存コードを再読するときの起点を明確にする

## まず全体像

```text
frontend (React + TypeScript)
  -> HTTP (/api/*)
backend/FinLearnApp.Api (Controller + DTO + InMemory Adapter)
  -> Application (UseCase/Command Handler)
  -> Interface (IActionExecutionStore)
backend/FinLearnApp.Api/Services/InMemoryActionExecutionStore (interface実装)
  -> backend/FinLearnApp.Api/Data/InMemoryStore (実データ)
src/Domain (Portfolio, Holding, Money などのルール)
```

## ルートフォルダごとの役割

- `backend/FinLearnApp.Api`
  - Web API本体（エンドポイント、DTO、DI設定、InMemoryデータ接続）
- `src/Application`
  - ユースケース層（コマンド、ハンドラー、抽象インターフェース）
- `src/Domain`
  - ドメインモデル層（エンティティ、値オブジェクト、ビジネスルール）
- `frontend`
  - React UI層（画面、APIクライアント、型定義）
- `docs`
  - 設計メモ、実装ステップ、運用ガイド

## バックエンドの責務分担

### 1. API入口（HTTP）
- `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
  - `POST /api/actions/buy-now|sell-now|wait` を受ける
  - Request DTO -> Command 変換
  - Handler結果を HTTPレスポンスへ変換
  - ターン不一致は `409 Conflict` を返す

- `backend/FinLearnApp.Api/Controllers/TickersController.cs`
  - 銘柄一覧/詳細の取得（読み取り）

- `backend/FinLearnApp.Api/Controllers/PortfoliosController.cs`
  - 投資家のポートフォリオ取得（読み取り）

### 2. DTOとレスポンス整形
- `backend/FinLearnApp.Api/Models/Api/ActionDtos.cs`
  - リクエスト: `expectedTurn` を受ける
  - レスポンス: `currentTurn` を返す

- `backend/FinLearnApp.Api/Models/Api/PortfolioDtos.cs`
  - `PortfolioDto` に `currentTurn` を含む

- `backend/FinLearnApp.Api/Responses/ApiProblemFactory.cs`
  - `400/404/409` の ProblemDetails生成

- `backend/FinLearnApp.Api/Mappers/PortfolioMapper.cs`
  - Domainの `Portfolio` を `PortfolioDto` に変換

### 3. データアクセス実装（InMemory）
- `backend/FinLearnApp.Api/Services/InMemoryActionExecutionStore.cs`
  - `IActionExecutionStore` の具体実装
  - Application層から見たときのデータ窓口

- `backend/FinLearnApp.Api/Data/InMemoryStore.cs`
  - 実データ本体（Companies/Tickers/Portfolios/Turn）
  - `GetCurrentTurn` / `AdvanceTurn` を提供

- `backend/FinLearnApp.Api/Data/SeedData.cs`
  - 起動時の初期データ作成

### 4. DI・起動設定
- `backend/FinLearnApp.Api/Program.cs`
  - DI登録
  - `IActionExecutionStore -> InMemoryActionExecutionStore` を紐づけ
  - MediatR/Serilog設定

## Application層の責務分担

- `src/Application/Actions/IActionExecutionStore.cs`
  - 抽象契約（ポートフォリオ取得、銘柄取得、ターン取得/進行）

- `src/Application/Actions/BuyNowCommand.cs`
- `src/Application/Actions/SellNowCommand.cs`
- `src/Application/Actions/WaitCommand.cs`
  - 入力コマンド（`ExpectedTurn` を含む）

- `src/Application/Actions/BuyNowCommandHandler.cs`
- `src/Application/Actions/SellNowCommandHandler.cs`
- `src/Application/Actions/WaitCommandHandler.cs`
  - ユースケース本体
  - `ExpectedTurn` と現在ターンを比較
  - 一致なら処理し `AdvanceTurn`
  - 不一致なら `Conflict`

- `src/Application/Actions/ActionExecutionResult.cs`
  - Handlerの戻り値
  - `Ok/BadRequest/NotFound/Conflict` と `CurrentTurn` を保持

## Domain層の責務分担

- `src/Domain/Entities/Portfolio.cs`
  - 保有/現金の増減
  - 評価額、損益計算

- `src/Domain/Entities/Holding.cs`
  - 銘柄ごとの数量管理

- `src/Domain/ValueObjects/Money.cs`
  - 通貨付き金額計算

- `src/Domain/ValueObjects/Ids.cs`
  - 型安全なID

## フロントエンドの責務分担

- `frontend/src/pages/Actions.tsx`
  - 画面状態（選択銘柄、数量、現在ターン）
  - 初期ロードで `tickers` と `portfolio` を取得
  - 送信時に `expectedTurn` をリクエストに設定
  - 成功時に `result.currentTurn` で画面ターン更新

- `frontend/src/api/actions.ts`
  - `/api/actions/*` 呼び出し関数

- `frontend/src/api/client.ts`
  - `fetchJson` 共通処理
  - 非2xxはエラーとして投げる

- `frontend/src/api/types.ts`
  - Request/Response DTO型（`expectedTurn`/`currentTurn` を含む）

## データフロー（クライアント起点）

## A. Actions画面の初期表示

1. 画面表示
- `frontend/src/pages/Actions.tsx`

2. 初期リクエスト
- `GET /api/tickers`
- `GET /api/portfolios/{investorId}`

3. バックエンド処理
- `TickersController` が銘柄一覧を返す
- `PortfoliosController` -> `PortfolioMapper` で `portfolio.currentTurn` を含めて返す

4. フロント状態更新
- `tickers` を表示
- `portfolio` を表示
- `setCurrentTurn(portfolioResult.currentTurn)`

## B. BuyNow/SellNow/Wait 実行

1. フロントで payload作成
- `expectedTurn = currentTurn`
- ファイル: `frontend/src/pages/Actions.tsx`

2. HTTP POST送信
- `frontend/src/api/actions.ts`

3. ControllerでCommand作成
- `ActionsController` が DTO -> Command 変換

4. Handlerでターン整合性チェック
- `currentTurn = _store.GetCurrentTurn(...)`
- `command.ExpectedTurn != currentTurn` なら `Conflict`

5. 一致時の処理
- Buy/Sell/Waitロジックを実行
- `AdvanceTurn` でターン +1
- `ActionExecutionResult.Ok(..., currentTurn)` を返す

6. ControllerがHTTPへ変換
- `Conflict` -> `409`
- `Ok` -> `ActionResultDto`（`currentTurn` 含む）

7. フロント反映
- `setPortfolio(result.portfolio)`
- `setCurrentTurn(result.currentTurn)`

## C. ターン不一致時（競合）

1. 送信時の `expectedTurn` が古い
2. Handlerが `Conflict` を返す
3. APIは `409 ProblemDetails` を返す
4. `fetchJson` はエラーをthrow
5. `Actions.tsx` はエラーメッセージ表示

補足:
- 現状は409時の自動再同期は未実装
- 必要なら「409受信 -> ポートフォリオ再取得 -> currentTurn更新 -> 再送」を追加可能

## この順でコードを読むと理解しやすい

1. `frontend/src/pages/Actions.tsx`
2. `frontend/src/api/actions.ts`
3. `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
4. `src/Application/Actions/*CommandHandler.cs`
5. `src/Application/Actions/IActionExecutionStore.cs`
6. `backend/FinLearnApp.Api/Services/InMemoryActionExecutionStore.cs`
7. `backend/FinLearnApp.Api/Data/InMemoryStore.cs`
8. `src/Domain/Entities/Portfolio.cs`

## いま未実装のポイント（次フェーズの目印）

- 永続DB実装（現在はInMemoryのみ）
- 409時のフロント自動リトライ/再同期
- ターンに応じた価格変動やフェーズ進行ルール
- FluentValidation/Zodによる入力バリデーション統合
