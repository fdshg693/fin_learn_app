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
| `Portfolio.cs` | ポートフォリオ | Cash + PositionSet, fill-based Buy/Sell logic |
| `Player.cs` | プレイヤー | Game participant identity (Name), order creation, profit/loss. Portfolio updates via `WithPortfolio` |
| `IExchange.cs` | 取引所 | Interface: price lookup + fee |
| `IOrderPlacer.cs` | 注文生成戦略 | Interface for order generation (DI point for testing) |
| `Game.cs` | ゲーム | State snapshot: turn, player, order book, instruments, **prices** |
| `ComputerTrader.cs` | コンピュータートレーダー | Implements `IOrderPlacer`. Generates 10 buy (85-105%) + 10 sell (95-115%) orders per turn |
| `Order.cs` | 注文 | ID, trader, instrument, side, quantity, price, stopPrice, createdAtTurn (注文作成ターン) |
| `OrderSide.cs` | 売買区分 | `Buy` / `Sell` enum |
| `OrderBook.cs` | 注文帳 | Order management + symmetric matching via `Match(Order)` + `ExpireOrders` for TTL-based expiration |
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

Each turn follows this sequence:
1. `IExchangeFactory.Create` constructs exchange from `Game.Prices` + `fee` parameter
2. `IOrderPlacer.PlaceOrders` generates computer orders using current prices
3. `Player.CreateOrder` creates the player's order (expressing intent)
4. `IMarket.Execute` matches the order against the OrderBook, returns `MatchResult`
5. `Portfolio.ApplyTrade` updates the portfolio, then `Player.WithPortfolio` creates updated player
6. `IPriceFluctuator.Fluctuate` applies random price changes for the next turn

**Responsibility boundaries:**
- **Player** — identity (Name), order creation (intent), profit/loss calculation. Does NOT know about OrderBook or trade execution
- **Portfolio** — pure asset management: cash, positions, trade application (`ApplyTrade`)
- **Market** — thin wrapper: calls `OrderBook.Match`, extracts the incoming order's `OrderFill`, builds `TradeResult`
- **TurnProcessor** — orchestrates the turn flow, connecting Player, Portfolio, Market, and OrderPlacer

`FillResult` contains per-order `OrderFill` entries + updated book. `TradeResult` is the player-facing result. `MatchResult` bridges them for Game's use.

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
