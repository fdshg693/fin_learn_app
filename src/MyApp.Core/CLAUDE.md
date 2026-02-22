## MyApp.Core — Domain Model

Pure domain layer with zero external dependencies. All types are **immutable sealed records/classes**.

### File Overview

| File | Domain | Description |
|---|---|---|
| `Instrument.cs` | 銘柄 | Stock identifier (value equality by ID) |
| `Position.cs` | ポジション | Instrument + quantity, evaluation amount |
| `PositionSet.cs` | ポジション集合 | Immutable collection, auto-aggregates same instrument via `+` |
| `Portfolio.cs` | ポートフォリオ | Cash + PositionSet, fill-based Buy/Sell logic |
| `Player.cs` | プレイヤー | Investor: creates orders, applies trade results, profit/loss calculation |
| `IExchange.cs` | 取引所 | Interface: price lookup + fee |
| `ExchangeExtensions.cs` | — | `TryGetPrice` safe helper |
| `IOrderPlacer.cs` | 注文生成戦略 | Interface for order generation (DI point for testing) |
| `Game.cs` | ゲーム | Turn-based progression orchestrating Player, Market, OrderBook |
| `ComputerTrader.cs` | コンピュータートレーダー | Implements `IOrderPlacer`. Generates 10 buy (95%) + 10 sell (100%) orders per turn |
| `Order.cs` | 注文 | ID, trader, instrument, side, quantity, price |
| `OrderSide.cs` | 売買区分 | `Buy` / `Sell` enum |
| `OrderBook.cs` | 注文帳 | Order management + symmetric matching via `Match(Order)` |
| `OrderFill.cs` | 注文約定明細 | Per-order fill result: order ID, filled quantity, total amount |
| `FillResult.cs` | 約定結果 | List of `OrderFill` per order ID + updated book. `GetFill(orderId)` for lookup |
| `IMarket.cs` | 市場 | Interface: order matching mediator between Player and OrderBook |
| `Market.cs` | 市場 | Default `IMarket` implementation using OrderBook |
| `TradeResult.cs` | 取引結果 | Player-facing fill result (no OrderBook knowledge) |
| `MatchResult.cs` | マッチング結果 | TradeResult + updated OrderBook (Game internal) |
| `Messages.cs` | — | Japanese error message constants |

### OrderBook Matching Rules

The `OrderBook` implements symmetric price-based order matching via a single `Match(Order incoming)` method:

- **Condition**: Buy price >= Sell price triggers a match
- **Contract price**: Always the **resting order's price** (the order already in the book)
  - Buy incoming, sell resting → contract price = sell order's price
  - Sell incoming, buy resting → contract price = buy order's price
- `Match(Order incoming)` — finds opposite-side eligible orders, matches at resting order's price, returns `FillResult` with per-order `OrderFill` entries

Sell orders are sorted ascending (cheapest first), buy orders descending (highest first). `TakeWhile` efficiently filters by price since lists are pre-sorted.

### Game Turn Flow

`Game` depends on `IOrderPlacer` and `IMarket` (both DI points), enabling test doubles.

Each turn follows this sequence:
1. `IOrderPlacer.PlaceOrders` generates computer orders and adds them to the `OrderBook`
2. `Player.CreateBuyOrder`/`CreateSellOrder` creates the player's order (expressing intent)
3. `IMarket.Execute` matches the order against the OrderBook, returns `MatchResult`
4. `Player.ApplyTrade` updates the portfolio from the structured `TradeResult`

**Responsibility boundaries:**
- **Player** — creates orders (intent), applies trade results (portfolio update). Does NOT know about OrderBook
- **Market** — thin wrapper: calls `OrderBook.Match`, extracts the incoming order's `OrderFill`, builds `TradeResult`
- **Game** — orchestrates the turn flow, connecting Player, Market, and OrderPlacer. Sets sell price = 1 (market order)

`FillResult` contains per-order `OrderFill` entries + updated book. `TradeResult` is the player-facing result. `MatchResult` bridges them for Game's use.

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
