## MyApp.Core — Domain Model

Pure domain layer with zero external dependencies. All types are **immutable sealed records/classes**.

### File Overview

| File | Domain | Description |
|---|---|---|
| `Instrument.cs` | 銘柄 | Stock identifier (value equality by ID) |
| `Position.cs` | ポジション | Instrument + quantity, evaluation amount |
| `PositionSet.cs` | ポジション集合 | Immutable collection, auto-aggregates same instrument via `+` |
| `Portfolio.cs` | ポートフォリオ | Cash + PositionSet, Buy/Sell trade logic |
| `Player.cs` | プレイヤー | Investor owning a Portfolio, profit/loss calculation |
| `IExchange.cs` | 取引所 | Interface: price lookup + fee |
| `ExchangeExtensions.cs` | — | `TryGetPrice` safe helper |
| `Game.cs` | ゲーム | Turn-based progression manager |
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

### Conventions

- Trade operations return `(T Result, string? Warning)` tuples — no exceptions for business rule violations
- `+` operator on Position/PositionSet for composition
- `WithQuantity()` internal method on Order for partial fill updates
