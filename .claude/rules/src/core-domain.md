---
paths:
  - "src/FinLearn.Core/**"
---

## FinLearn.Core — Domain Model

Pure domain layer with zero external dependencies. All types are **immutable sealed records/classes**.

### File Overview

| File | Domain | Description |
|---|---|---|
| `Instrument.cs` | 銘柄 | Stock identifier (value equality by ID) |
| `Position.cs` | ポジション | Instrument + quantity, evaluation amount |
| `PositionSet.cs` | ポジション集合 | Immutable collection, auto-aggregates same instrument via `+` |
| `Portfolio.cs` | ポートフォリオ | Available + reserved の cash / positions を保持。指値発注時に `ReserveBuy` / `ReserveSell` で available → reserved に移し、約定で `SettleReservedBuy` / `SettleReservedSell` で確定（買いは差額返金）、失効で `ReleaseBuyReservation` / `ReleaseSellReservation` で解放。成行 fill は既存の `ApplyTrade`。`Portfolio.CreateInfinite()` は予約系を含めて全 no-op（computer 用） |
| `Player.cs` | プレイヤー | Game participant identity (Name), order creation, profit/loss. Portfolio updates via `WithPortfolio` |
| `IExchange.cs` | 取引所 | Interface: price lookup + fee |
| `IOrderPlacer.cs` | 注文生成戦略 | Interface for order generation (DI point for testing). 戻り値は `OrderPlacementResult`（UpdatedBook, NextOrderId, PlacedOrders, **Fills**, **UpdatedTraderPortfolios**） |
| `SettlementProcessor.cs` | 約定確定 | Static service: 当ターン発生した全 `OrderFill` を `traderId → Portfolio` 統一マップに settlement する。`SettleFills` / `ReleaseExpired` / `ComputePostFillRemainingQty`。Limit は予約から確定、Market は `ApplyTrade`。`feeIfFinal` は per-order fee（完全消化の fill のみ計上） |
| `Game.cs` | ゲーム | State snapshot: turn, player, order book, instruments, **prices**, **computerPortfolios** (computer1〜10 の Portfolio) |
| `ComputerTrader.cs` | コンピュータートレーダー | Implements `IOrderPlacer`. 10 仮想プレイヤー (`computer1`〜`computer10`) each place 1 buy (85-105%) + 1 sell (95-115%) per turn (20 orders total). 発注時に `ReserveBuy` / `ReserveSell`（Infinite なので no-op）、約定の Portfolio 反映は `SettlementProcessor.SettleFills` に委譲（player の resting 注文と約定したケースもここで反映される）。`IsComputerTrader(string)` identifies any computer trader id |
| `Order.cs` | 注文 | ID, trader, instrument, side, quantity, price, stopPrice, createdAtTurn (作成ターン), expiresAtTurn (有効期限ターン・絶対値) |
| `OrderSide.cs` | 売買区分 | `Buy` / `Sell` enum |
| `OrderBook.cs` | 注文帳 | Order management + symmetric matching via `Match(Order)` + `ExpireOrders(currentTurn)` returns `(Updated, Expired)` tuple (per-order `ExpiresAtTurn` based). Expired リストは予約解放のため公開 |
| `OrderFill.cs` | 注文約定明細 | Per-order fill result: order ID, filled quantity, total amount |
| `FillResult.cs` | 約定結果 | List of `OrderFill` per order ID + updated book. `GetFill(orderId)` for lookup |
| `IMarket.cs` | 市場 | Interface: order matching mediator between Player and OrderBook |
| `Market.cs` | 市場 | Default `IMarket` implementation using OrderBook |
| `TradeResult.cs` | 取引結果 | Player-facing fill result (no OrderBook knowledge) |
| `MatchResult.cs` | マッチング結果 | TradeResult + updated OrderBook (Game internal) |
| `Messages.cs` | — | Japanese error message constants |
| `IPriceFluctuator.cs` | 株価変動戦略 | Interface: price fluctuation strategy (DI point) |
| `RandomPriceFluctuator.cs` | ランダム株価変動 | ±5% per turn, floor of 1. Takes `Random` for deterministic tests |
| `SimpleExchange.cs` | 簡易取引所 | `IExchange` impl from price dictionary + fee. Used internally by `TurnProcessor` |

### OrderBook Matching Rules

The `OrderBook` implements symmetric price-based order matching via a single `Match(Order incoming)` method:

- **Condition**: Buy price >= Sell price triggers a match
- **Contract price**: Always the **resting order's price** (the order already in the book)
  - Buy incoming, sell resting → contract price = sell order's price
  - Sell incoming, buy resting → contract price = buy order's price
- `Match(Order incoming)` — finds opposite-side eligible orders, matches at resting order's price, returns `FillResult` with per-order `OrderFill` entries

Sell orders are sorted ascending (cheapest first), buy orders descending (highest first). `TakeWhile` efficiently filters by price since lists are pre-sorted.

### Game Turn Flow

`TurnProcessor` depends on `IOrderPlacer`, `IMarket`, and `IPriceFluctuator` (all DI points). Method signatures use `int fee` instead of `IExchange` — prices come from `Game.Prices`.

各ターンは「注文生成（intent）」と「マーケット結果反映（settlement）」を分離した形で進行する：

1. `IExchangeFactory.Create` で exchange を構築
2. `BuildAllPortfolios` で `traderId → Portfolio` 統合 view を作る（player + computer1〜10）
3. `IOrderPlacer.PlaceOrders` が computer 注文の発注 + 約定 + settlement を完結（`SettlementProcessor.SettleFills` 経由で player resting への約定も反映）
4. `Player.CreateOrder` で player 注文を作成
5. **指値**の場合のみ `Portfolio.ReserveBuy` / `ReserveSell` で事前予約。失敗（残高/数量不足）なら Wait 化 + warning（computer settlement は確定維持）
6. `IMarket.Execute` で player 注文を約定
7. **指値** fill は `SettlementProcessor.SettleFills`（予約消費 + 差額返金、失敗パス無し）。**成行** fill は同期 `ApplyTrade`（残高不足時は `matchResult.Fills` を破棄してロールバック、computer settlement は確定維持）
8. 指値の未約定分を板に追加
9. `OrderBook.ExpireOrders` で失効注文を抽出 → `SettlementProcessor.ReleaseExpired` で予約解放 → `IPriceFluctuator.Fluctuate` で価格変動 → `SplitPortfolios` で Player/ComputerPortfolios に分解

**責務分離（Reservation Model）:**
- **注文生成（intent）** — 当ターンの新規注文を作る入力依存処理
- **Settlement** — 当ターン発生した全 `OrderFill` を `traderId` 別 Portfolio に統一適用する入力非依存処理。`SettlementProcessor` に集約され、computer 同士・computer-vs-player resting・player の incoming/resting 全てを同じ仕組みで反映

**Responsibility boundaries:**
- **Player** — identity (Name), order creation (intent), profit/loss calculation. Does NOT know about OrderBook or trade execution
- **Portfolio** — 資産管理 (available + reserved の cash/positions)、予約系メソッド、約定確定、解放
- **SettlementProcessor** — 全 fill を統一的に Portfolio へ反映 + 失効注文の予約解放
- **ComputerTrader** — 注文生成 + 自身の予約呼び出し + その場の matching → settlement 委譲
- **Market** — thin wrapper: calls `OrderBook.Match`, extracts the incoming order's `OrderFill`, builds `TradeResult`
- **TurnProcessor** — turn flow オーケストレーション、player の予約と settlement の橋渡し、失効処理

**重要な不変条件:**
- 指値発注時に予約成功 → 約定の settlement は決して失敗しない（残高不足等の Warning は出ない）
- per-order fee（注文単位で1回。完全消化の fill で計上、部分約定は fee=0）
- `Portfolio.Cash` は available のみを表す。総資産は `Cash + ReservedCash + 全保有評価`
- 成行ロールバックの対象は player の market fill のみ。Computer 同士・computer-vs-player resting の settlement は確定事実として維持

`FillResult` contains per-order `OrderFill` entries + updated book. `TradeResult` is the player-facing result. `MatchResult` bridges them for Game's use.

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
- `OrderBook.ExpireOrders` returns `(Updated, Expired)` — caller must release reservations for expired orders via `SettlementProcessor.ReleaseExpired`
