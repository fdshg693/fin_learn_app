## Architecture

- **Framework:** .NET 9, C# with nullable reference types
- **Test framework:** xUnit
- **Solution:** `fin_learn_app.sln` with two projects:
  - `src/MyApp.Core` — Domain model (no external dependencies)
  - `tests/MyApp.Tests` — Unit tests

### Domain Model

All domain objects are **immutable** (sealed classes). Operations return new instances rather than mutating state. Trading operations return `(T Result, string? Warning)` tuples for error handling without exceptions.

Key models (see `docs/DDD.md` for full domain glossary):
- **Instrument (銘柄)** — Stock identifier
- **Position (ポジション)** — Holding of an instrument with quantity
- **PositionSet (ポジション集合)** — Immutable collection that normalizes duplicate instruments by aggregating quantities
- **Portfolio (ポートフォリオ)** — Aggregate root: cash + positions, with Buy/Sell trade logic
- **Player (プレイヤー)** — Investor who owns a portfolio
- **Game (ゲーム)** — Pure state snapshot: turn, player, order book, instruments (no workflow logic)
- **TurnProcessor (ターン処理)** — Domain service: orchestrates turn progression (Buy/Sell/Wait actions, computer order generation, market matching, portfolio update)
- **IExchange (取引所)** — Interface for price lookups (dependency injection)
- **Order (注文)** — Buy/sell order with trader, instrument, quantity, price
- **OrderBook (注文帳)** — Immutable order book with price-based matching (see `src/MyApp.Core/CLAUDE.md` for details)
- **FillResult (約定結果)** — Result of order matching: filled quantity, total amount, updated book

### Design Patterns

- Operator overloading (`+`) for composing Position/PositionSet objects
- `IExchange` interface with `TestExchange` test double for unit testing
- Currency is JPY-only
