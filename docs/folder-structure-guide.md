# フォルダ構成ガイド（クライアント起点のデータフロー付き）

更新日: 2026-05-10

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
library/Domain (Portfolio, Holding, Money, Exchange, TurnDomainService などのルール)
```

## ルートフォルダごとの役割

- `backend/FinLearnApp.Api`
  - Web API本体（エンドポイント、DTO、DI設定、InMemoryデータ接続）
- `library/Application`
  - ユースケース層（コマンド、ハンドラー、抽象インターフェース）
- `library/Domain`
  - ドメインモデル層（エンティティ、値オブジェクト、ビジネスルール）
- `frontend`
  - React UI層（画面、APIクライアント、型定義）
- `docs`
  - 設計メモ、実装ステップ、運用ガイド

## バックエンドの責務分担

### 1. API入口（HTTP）
- `backend/FinLearnApp.Api/Controllers/ActionsController.cs`
  - `POST /api/actions/buy|sell|wait` を受ける
  - Request DTO -> Command 変換
  - Handler結果を HTTPレスポンスへ変換
  - ターン不一致は `409 Conflict` を返す

- `backend/FinLearnApp.Api/Controllers/TickersController.cs`
  - 銘柄一覧/詳細の取得（読み取り）

- `backend/FinLearnApp.Api/Controllers/PortfoliosController.cs`
  - 投資家のポートフォリオ取得（読み取り）

- `backend/FinLearnApp.Api/Controllers/MarketController.cs`
  - 注文票と約定履歴のスナップショット取得（`GET /api/market/snapshot`）

### 2. DTOとレスポンス整形
- `backend/FinLearnApp.Api/Models/Api/ActionDtos.cs`
  - リクエスト: `expectedTurn` を受ける
  - `buy` / `sell` は `limitPrice` の有無で成行/指値を切り替える
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
  - 実データ本体（Companies/Tickers/Portfolios/Turn/Exchange）
  - `GetCurrentTurn` / `AdvanceTurn` を提供
  - 市場価格の取得と Domain への委譲を担当する薄いストア

- `library/Domain/Services/TurnDomainService.cs`
  - ターン進行時の株価変動、システム注文生成、クロス注文解消

- `library/Domain/Entities/Exchange.cs`
  - BuyNow / SellNow / BuyLimit / SellLimit の即時マッチング
  - 約定履歴 `Trades` を保持

- `backend/FinLearnApp.Api/Data/SeedData.cs`
  - 起動時の初期データ作成

### 4. DI・起動設定
- `backend/FinLearnApp.Api/Program.cs`
  - DI登録
  - `IActionExecutionStore -> InMemoryActionExecutionStore` を紐づけ
  - MediatR/Serilog設定

## Application層の責務分担

- `library/Application/Actions/IActionExecutionStore.cs`
  - 抽象契約（ポートフォリオ取得、銘柄取得、ターン取得/進行）

- `library/Application/Actions/BuyNowCommand.cs`
- `library/Application/Actions/BuyLimitCommand.cs`
- `library/Application/Actions/SellNowCommand.cs`
- `library/Application/Actions/SellLimitCommand.cs`
- `library/Application/Actions/WaitCommand.cs`
  - 入力コマンド（`ExpectedTurn` を含む）

- `library/Application/Actions/BuyNowCommandHandler.cs`
- `library/Application/Actions/BuyLimitCommandHandler.cs`
- `library/Application/Actions/SellNowCommandHandler.cs`
- `library/Application/Actions/SellLimitCommandHandler.cs`
- `library/Application/Actions/WaitCommandHandler.cs`
  - ユースケース本体
  - `ExpectedTurn` と現在ターンを比較
  - 一致なら処理し `AdvanceTurn`
  - 不一致なら `Conflict`

- `library/Application/Actions/ActionExecutionResult.cs`
  - Handlerの戻り値
  - `Ok/BadRequest/NotFound/Conflict` と `CurrentTurn` を保持

## Domain層の責務分担

- `library/Domain/Entities/Portfolio.cs`
  - 保有/現金の増減
  - 評価額、損益計算

- `library/Domain/Entities/Holding.cs`
  - 銘柄ごとの数量管理

- `library/Domain/ValueObjects/Money.cs`
  - 通貨付き金額計算

- `library/Domain/ValueObjects/Ids.cs`
  - 型安全なID

- `library/Domain/Entities/Exchange.cs`
  - 注文板を持ち、即時注文の価格優先・時間優先マッチングを実行する

- `library/Domain/Services/TurnDomainService.cs`
  - ターン進行の手順をまとめ、価格変動、システム注文生成、クロス注文解消を順に適用する

## フロントエンドの責務分担

- `frontend/src/pages/Actions.tsx`
  - 画面状態（選択銘柄、数量、現在ターン）
  - 初期ロードで `tickers` と `portfolio` を取得
  - 送信時に `expectedTurn` をリクエストに設定
  - `buy` / `sell` の共通送信関数で `limitPrice` の有無を切り替える
  - 成功時に `result.currentTurn` で画面ターン更新

- `frontend/src/api/actions.ts`
  - `/api/actions/buy` `/api/actions/sell` `/api/actions/wait` 呼び出し関数

- `frontend/src/api/client.ts`
  - `fetchJson` 共通処理
  - 非2xxはエラーとして投げる

- `frontend/src/api/types.ts`
  - Request/Response DTO型（`expectedTurn`/`currentTurn` を含む）

- `frontend/src/api/market.ts`
  - `GET /api/market/snapshot` 呼び出し

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

## B. Buy/Sell/Wait 実行

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
- `AdvanceTurn` 内で価格変動、システム注文生成、クロス注文解消が走る
- `ActionExecutionResult.Ok(..., currentTurn)` を返す

6. ControllerがHTTPへ変換
- `Conflict` -> `409`
- `Ok` -> `ActionResultDto`（`currentTurn` 含む）

7. フロント反映
- `setPortfolio(result.portfolio)`
- `setCurrentTurn(result.currentTurn)`

## B-2. 約定ルール（価格優先/時間優先）

現在の `BuyNow` / `SellNow` / `BuyLimit` / `SellLimit` は、注文票にある既存注文と即時マッチングします。

- BuyNow
  - 対象: 同一銘柄の `SellOrders`
  - 条件: `order.Price <= ticker.CurrentPrice`
  - 優先順: 価格の安い順 -> 同価格なら古い順

- SellNow
  - 対象: 同一銘柄の `BuyOrders`
  - 条件: `order.Price >= ticker.CurrentPrice`
  - 優先順: 価格の高い順 -> 同価格なら古い順

- 共通
  - 部分約定あり（注文数量が足りない場合）
  - 約定した分だけ `OrderBook` から減算
  - 残量があれば注文票に残る
  - 約定履歴は `Trades` に記録される
  - 指値注文は `limitPrice` 条件で候補を絞るが、約定価格は相手注文の価格を使う

ファイル参照:
- `backend/FinLearnApp.Api/Data/InMemoryStore.cs`
- `library/Domain/Entities/Exchange.cs`
- `library/Application/Actions/BuyNowCommandHandler.cs`
- `library/Application/Actions/BuyLimitCommandHandler.cs`
- `library/Application/Actions/SellNowCommandHandler.cs`
- `library/Application/Actions/SellLimitCommandHandler.cs`

## B-3. 約定処理フロー（図）

```text
Actions.tsx
  -> POST /api/actions/buy (expectedTurn付き)
ActionsController
  -> IMediator.Send(BuyNowCommand)
BuyNowCommandHandler
  -> IActionExecutionStore.ExecuteBuyNow(...)
InMemoryActionExecutionStore
  -> InMemoryStore.ExecuteBuyNow(...)
InMemoryStore
  -> Exchange.ExecuteBuyNow(...) へ委譲
Exchange
  -> OrderBook(Sell) から候補抽出
  -> 価格/時間優先で約定計算
  -> Trade記録
  -> OrderBook残量更新
  -> OrderMatchResult返却
BuyNowCommandHandler
  -> Portfolio(現金/保有)更新
  -> AdvanceTurn(株価変動 + システム注文生成 + クロス注文解消)
ActionsController
  -> ActionResultDto(currentTurn付き)
Actions.tsx
  -> 画面反映 + /api/market/snapshot再取得
```

## D. 市場スナップショット表示フロー

1. `Actions.tsx` が `fetchMarketSnapshot()` を呼ぶ
2. `GET /api/market/snapshot` を実行
3. `MarketController` が `OrderBook` と `Trades` を DTO化して返す
4. フロントで「注文票（買い/売り）」と「約定履歴」を表示

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
4. `library/Application/Actions/*CommandHandler.cs`
5. `library/Application/Actions/IActionExecutionStore.cs`
6. `backend/FinLearnApp.Api/Services/InMemoryActionExecutionStore.cs`
7. `backend/FinLearnApp.Api/Data/InMemoryStore.cs`
8. `library/Domain/Entities/Portfolio.cs`

## いま未実装のポイント（次フェーズの目印）

- 永続DB実装（現在はInMemoryのみ）
- 409時のフロント自動リトライ/再同期
- 注文の失効/キャンセル、注文票の上限管理
- FluentValidation/Zodによる入力バリデーション統合
