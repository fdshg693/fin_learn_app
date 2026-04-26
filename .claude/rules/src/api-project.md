---
paths:
  - "src/FinLearn.Api/**"
---

## FinLearn.Api — REST API (Minimal API)

ASP.NET Core Minimal API layer. Translates HTTP requests into `TurnProcessor` calls and maps domain objects to JSON DTOs.

### File Overview

| File | Description |
|---|---|
| `Program.cs` | Endpoint definitions (5 routes), DI registration, CORS, `ToResponse` / `ProcessOrder` helpers |
| `Dtos/GameResponse.cs` | Response DTOs: `GameResponse`, `PlayerDto`, `PositionDto`, `InstrumentDto` |
| `Dtos/OrderRequest.cs` | Request DTO: `OrderRequest` (instrumentId, quantity, price?) |
| `Services/GameConfig.cs` | Game defaults (銘柄数, 初期株価, 手数料) |
| `Services/GameStore.cs` | In-memory game state (`ConcurrentDictionary<string, Game>`) |

### Endpoints

| Method | Path | Action |
|---|---|---|
| POST | `/api/games` | `GameStore.CreateGame` → 201 Created |
| GET | `/api/games/{id}` | `GameStore.GetGame` → 200 / 404 |
| POST | `/api/games/{id}/buy` | `TurnProcessor.Buy` via `ProcessOrder` → 200 / 404 |
| POST | `/api/games/{id}/sell` | `TurnProcessor.Sell` via `ProcessOrder` → 200 / 404 |
| POST | `/api/games/{id}/wait` | `TurnProcessor.Wait` → 200 / 404 |

### DI Configuration

| Service | Lifetime | Notes |
|---|---|---|
| `GameConfig` | Singleton | Game defaults (銘柄数, 初期株価, 手数料) |
| `GameStore` | Singleton | In-memory state, depends on `GameConfig` |
| `IExchangeFactory` | Singleton | `SimpleExchangeFactory` — DTO mapping時の価格評価に使用 |
| `Random` | Singleton | Shared by ComputerTrader and RandomPriceFluctuator |
| `TurnProcessor` | Transient | Composed with `ComputerTrader` + `RandomPriceFluctuator` |

### Game Defaults (`GameConfig`)

- 銘柄数: 3 (ID: 1, 2, 3)
- 初期株価: 各 100 JPY
- 手数料: 10 JPY
- プレイヤー初期資金: 10,000 JPY (`Player` 内の定数)

### Design Decisions

- **Warning handling**: Domain returns `(Game, string? Warning)` tuples. API always returns 200 OK — `warning` field is `null` on success, contains a message on failure. Turn does not advance when warning is present, and `GameStore` is not updated
- **ProcessOrder helper**: buy/sell の共通ロジック（ゲーム取得 → アクション実行 → ストア更新 → レスポンス生成）を `ProcessOrder` static メソッドに集約。アクション部分だけを `Func` で差し替え
- **DTO mapping**: `ToResponse` static method in `Program.cs` converts `Game` → `GameResponse`. `IExchangeFactory` 経由で価格評価用の exchange を生成
- **Game ID**: `Guid.NewGuid().ToString("N")` — 32-char hex string, URL-friendly
- **CORS**: Allows React dev server at `localhost:5173`
- **`public partial class Program { }`**: Enables `WebApplicationFactory<Program>` in integration tests
