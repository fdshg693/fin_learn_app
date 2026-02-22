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
| `OrderBook.cs` | 注文帳 | Order management + execution matching |
| `FillResult.cs` | 約定結果 | Filled quantity, total amount, updated book (OrderBook internal) |
| `IMarket.cs` | 市場 | Interface: order matching mediator between Player and OrderBook |
| `Market.cs` | 市場 | Default `IMarket` implementation using OrderBook |
| `TradeResult.cs` | 取引結果 | Player-facing fill result (no OrderBook knowledge) |
| `MatchResult.cs` | マッチング結果 | TradeResult + updated OrderBook (Game internal) |
| `Messages.cs` | — | Japanese error message constants |

### OrderBook Matching Rules

The `OrderBook` implements price-based order matching per `docs/DDD.md`:

- **Condition**: Buy price >= Sell price triggers a match
- **Contract price**: Always the **sell order's price**
- `FillBuy(instrumentId, quantity, buyPrice)` — matches sell orders where `sellPrice <= buyPrice`, cheapest first. Contract at each sell order's price
- `FillSell(instrumentId, quantity, sellPrice)` — matches buy orders where `buyPrice >= sellPrice`, highest first. Contract at the incoming sell price

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
- **Market** — mediates between orders and OrderBook. Determines execution price (e.g., best buy price for sells)
- **Game** — orchestrates the turn flow, connecting Player, Market, and OrderPlacer

`FillResult` is internal to OrderBook/Market. `TradeResult` is the player-facing result. `MatchResult` bridges them for Game's use.

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
