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
| `Dtos/GameResponse.cs` | Response DTOs: `GameResponse`, `PlayerDto`, `PositionDto`, `PendingOrderDto`, `InstrumentDto`, `TradeResultDto` |
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

- **エラー応答の二系統**:
  - **形式不正 → 400 BadRequest**: `side` 未指定、`quantity <= 0`、`price <= 0`、`stopPrice <= 0`。`PlaceOrder` ハンドラ冒頭で弾く。クライアントの不正リクエストに対する REST 慣習に沿った応答。
  - **ゲーム状態依存の失敗 → 200 OK + `warning` フィールド**: 保有不足、現金不足、約定ゼロ等。Domain の `(Game, string? Warning)` タプルから `GameResponse.warning` にマップ。`warning` が null 以外のとき `GameStore` は更新しない。
- **Domain の多重防御**: API 層で形式不正を弾いた上で、`TurnProcessor.Buy/Sell` も同条件で `Rejected()` を返す safety net を持つ。Domain 層を直接呼ぶテストや将来の他経路に対する自律性を保つため。
- **ProcessOrder helper**: 注文処理の共通ロジック（ゲーム取得 → アクション実行 → ストア更新 → レスポンス生成）を `ProcessOrder` static メソッドに集約。`PlaceOrder` ハンドラが `request.Side` で `processor.Buy` / `processor.Sell` を switch して `Func` として渡す。
- **Enum JSON binding**: `Program.cs` で `JsonStringEnumConverter` をグローバル登録。`OrderRequest.Side` は `OrderSide?` 型で、JSON では `"Buy"` / `"Sell"` 文字列。null（未指定）は handler が 400 を返し、`"Hold"` などの不正値は ASP.NET Core のモデルバインドが自動で 400 を返す
- **DTO mapping**: `ToResponse` static method in `Program.cs` converts `Game` → `GameResponse`. `IExchangeFactory` 経由で価格評価用の exchange を生成
- **Game ID**: `Guid.NewGuid().ToString("N")` — 32-char hex string, URL-friendly
- **CORS**: Allows React dev server at `localhost:5173`
- **`public partial class Program { }`**: Enables `WebApplicationFactory<Program>` in integration tests
