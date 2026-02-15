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
- **IExchange (取引所)** — Interface for price lookups (dependency injection)

### Design Patterns

- Operator overloading (`+`) for composing Position/PositionSet objects
- `IExchange` interface with `TestExchange` test double for unit testing
- Currency is JPY-only
