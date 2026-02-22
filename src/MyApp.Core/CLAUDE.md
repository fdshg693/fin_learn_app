## MyApp.Core — Domain Model

Pure domain layer with zero external dependencies. All types are **immutable sealed records/classes**.

### File Overview

| File | Domain | Description |
|---|---|---|
| `Instrument.cs` | 銘柄 | Stock identifier (value equality by ID) |
| `Position.cs` | ポジション | Instrument + quantity, evaluation amount |
| `PositionSet.cs` | ポジション集合 | Immutable collection, auto-aggregates same instrument via `+` |
| `Portfolio.cs` | ポートフォリオ | Cash + PositionSet, fill-based Buy/Sell logic |
| `Player.cs` | プレイヤー | Investor owning a Portfolio, profit/loss calculation |
| `IExchange.cs` | 取引所 | Interface: price lookup + fee |
| `ExchangeExtensions.cs` | — | `TryGetPrice` safe helper |
| `IOrderPlacer.cs` | 注文生成戦略 | Interface for order generation (DI point for testing) |
| `Game.cs` | ゲーム | Turn-based progression with OrderBook + IOrderPlacer |
| `ComputerTrader.cs` | コンピュータートレーダー | Implements `IOrderPlacer`. Generates 10 buy (95%) + 10 sell (100%) orders per turn |
| `Order.cs` | 注文 | ID, trader, instrument, side, quantity, price |
| `OrderSide.cs` | 売買区分 | `Buy` / `Sell` enum |
| `OrderBook.cs` | 注文帳 | Order management + execution matching |
| `FillResult.cs` | 約定結果 | Filled quantity, total amount, updated book |
| `Messages.cs` | — | Japanese error message constants |

### OrderBook Matching Rules

The `OrderBook` implements price-based order matching per `docs/DDD.md`:

- **Condition**: Buy price >= Sell price triggers a match
- **Contract price**: Always the **sell order's price**
- `FillBuy(instrumentId, quantity, buyPrice)` — matches sell orders where `sellPrice <= buyPrice`, cheapest first. Contract at each sell order's price
- `FillSell(instrumentId, quantity, sellPrice)` — matches buy orders where `buyPrice >= sellPrice`, highest first. Contract at the incoming sell price

Sell orders are sorted ascending (cheapest first), buy orders descending (highest first). `TakeWhile` efficiently filters by price since lists are pre-sorted.

### Game Turn Flow (with IOrderPlacer)

`Game` depends on `IOrderPlacer` (not concrete `ComputerTrader`), enabling test doubles for order generation.

Each turn follows this sequence:
1. `IOrderPlacer.PlaceOrders` generates orders and adds them to the `OrderBook`
2. Player action is executed against the updated OrderBook:
   - **Buy**: `FillBuy` matches player's buy against sell orders
   - **Sell**: `FillSell` matches player's sell against buy orders
   - **Wait**: No player action, OrderBook accumulates orders
3. `Player.Buy`/`Sell` updates the portfolio based on the `FillResult`

All trades go through the OrderBook fill path — `Portfolio.Buy(instrumentId, filledQuantity, totalCost, fee)` and `Portfolio.Sell(instrumentId, filledQuantity, totalProceeds, fee)`. There is no direct-price trade path.

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
