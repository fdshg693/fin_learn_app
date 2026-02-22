## FinLearn.Api — REST API (Minimal API)

ASP.NET Core Minimal API layer. Translates HTTP requests into `TurnProcessor` calls and maps domain objects to JSON DTOs.

### File Overview

| File | Description |
|---|---|
| `Program.cs` | Endpoint definitions (5 routes), DI registration, CORS, `ToResponse` DTO mapping |
| `Dtos/GameResponse.cs` | Response DTOs: `GameResponse`, `PlayerDto`, `PositionDto`, `InstrumentDto` |
| `Dtos/OrderRequest.cs` | Request DTO: `OrderRequest` (instrumentId, quantity, price?) |
| `Services/GameStore.cs` | In-memory game state (`ConcurrentDictionary<string, Game>`), game defaults |

### Endpoints

| Method | Path | Action |
|---|---|---|
| POST | `/api/games` | `GameStore.CreateGame` → 201 Created |
| GET | `/api/games/{id}` | `GameStore.GetGame` → 200 / 404 |
| POST | `/api/games/{id}/buy` | `TurnProcessor.Buy` → 200 (with optional `warning`) / 404 |
| POST | `/api/games/{id}/sell` | `TurnProcessor.Sell` → 200 (with optional `warning`) / 404 |
| POST | `/api/games/{id}/wait` | `TurnProcessor.Wait` → 200 / 404 |

### DI Configuration

| Service | Lifetime | Notes |
|---|---|---|
| `GameStore` | Singleton | In-memory state, no DB |
| `Random` | Singleton | Shared by ComputerTrader and RandomPriceFluctuator |
| `TurnProcessor` | Transient | Composed with `ComputerTrader` + `RandomPriceFluctuator` |

### Game Defaults (`GameStore` constants)

- 銘柄数: 3 (ID: 1, 2, 3)
- 初期株価: 各 100 JPY
- 手数料: 10 JPY (`GameStore.Fee`)
- プレイヤー初期資金: 10,000 JPY (`Player` 内の定数)

### Design Decisions

- **Warning handling**: Domain returns `(Game, string? Warning)` tuples. API always returns 200 OK — `warning` field is `null` on success, contains a message on failure. Turn does not advance when warning is present, and `GameStore` is not updated
- **DTO mapping**: `ToResponse` static method in `Program.cs` converts `Game` → `GameResponse`. Uses `SimpleExchange` to resolve current prices for position evaluation and instrument display
- **Game ID**: `Guid.NewGuid().ToString("N")` — 32-char hex string, URL-friendly
- **CORS**: Allows React dev server at `localhost:5173`
- **`public partial class Program { }`**: Enables `WebApplicationFactory<Program>` in integration tests
