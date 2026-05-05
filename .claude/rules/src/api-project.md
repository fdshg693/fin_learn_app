---
paths:
  - "src/FinLearn.Api/**"
---

## FinLearn.Api — REST API (Minimal API)

ASP.NET Core Minimal API layer. Translates HTTP requests into `TurnProcessor` calls and maps domain objects to JSON DTOs.

### File Overview

| File | Description |
|---|---|
| `Program.cs` | DI registration, CORS, `JsonStringEnumConverter` global config, Serilog setup |
| `Endpoints/GameEndpoints.cs` | 4 ゲーム系ルート定義 + `ProcessOrder` / `PlaceOrder` ヘルパー |
| `Endpoints/AdminEndpoints.cs` | 管理用ルート（`/api/admin/games/{id}/orderbook` 等） |
| `Dtos/GameResponse.cs` | Response DTOs: `GameResponse`, `PlayerDto`, `PositionDto`, `InstrumentDto` |
| `Dtos/OrderRequest.cs` | Request DTO: `OrderRequest` (side, instrumentId, quantity, price?, stopPrice?) |
| `Services/GameConfig.cs` | Game defaults (銘柄数, 初期株価, 手数料) |
| `Services/GameStore.cs` | In-memory game state (`ConcurrentDictionary<string, Game>`) |

### Endpoints

| Method | Path | Action |
|---|---|---|
| POST | `/api/games` | `GameStore.CreateGame` → 201 Created |
| GET | `/api/games/{id}` | `GameStore.GetGame` → 200 / 404 |
| POST | `/api/games/{id}/orders` | `TurnProcessor.Buy`/`Sell` (body の `side` で分岐) via `ProcessOrder` → 200 / 400 / 404 |
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
- **ProcessOrder helper**: 注文処理の共通ロジック（ゲーム取得 → アクション実行 → ストア更新 → レスポンス生成）を `ProcessOrder` static メソッドに集約。`PlaceOrder` ハンドラが `request.Side` で `processor.Buy` / `processor.Sell` を switch して `Func` として渡す。`side` が未指定の場合は handler 冒頭で 400 BadRequest
- **Enum JSON binding**: `Program.cs` で `JsonStringEnumConverter` をグローバル登録。`OrderRequest.Side` は `OrderSide?` 型で、JSON では `"Buy"` / `"Sell"` 文字列。null（未指定）は handler が 400 を返し、`"Hold"` などの不正値は ASP.NET Core のモデルバインドが自動で 400 を返す
- **DTO mapping**: `ToResponse` static method in `Program.cs` converts `Game` → `GameResponse`. `IExchangeFactory` 経由で価格評価用の exchange を生成
- **Game ID**: `Guid.NewGuid().ToString("N")` — 32-char hex string, URL-friendly
- **CORS**: Allows React dev server at `localhost:5173`
- **`public partial class Program { }`**: Enables `WebApplicationFactory<Program>` in integration tests
