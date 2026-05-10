---
paths:
  - "src/FinLearn.Api/**"
---

## FinLearn.Api — REST API (Minimal API) + In-Process HTMX Frontend

ASP.NET Core Minimal API layer. Translates HTTP requests into `TurnProcessor` calls and maps domain objects to JSON DTOs. Also hosts an in-process Razor Pages / HTMX frontend at `/play` alongside the JSON API at `/api`.

### File Overview

| File | Description |
|---|---|
| `Program.cs` | DI registration, CORS, `JsonStringEnumConverter` global config, Serilog setup, Razor Pages + static files |
| `Endpoints/GameEndpoints.cs` | 4 ゲーム系ルート定義 + `ProcessOrder` / `PlaceOrder` ヘルパー |
| `Endpoints/AdminEndpoints.cs` | 管理用ルート（`/api/admin/games/{id}/orderbook` 等） |
| `Dtos/GameResponse.cs` | Response DTOs: `GameResponse`, `PlayerDto`, `PositionDto`, `PendingOrderDto`, `InstrumentDto`, `TradeResultDto` |
| `Dtos/OrderRequest.cs` | Request DTO: `OrderRequest` (side, instrumentId, quantity, price?, stopPrice?) |
| `Services/GameConfig.cs` | Game defaults (銘柄数, 初期株価, 手数料) |
| `Services/GameStore.cs` | In-memory game state (`ConcurrentDictionary<string, Game>`) |
| `Pages/Play/Index.cshtml(.cs)` | `/play` ホーム — POST → `GameStore.CreateGame` → 302 to `/play/{id}` |
| `Pages/Play/Game.cshtml(.cs)` | `/play/{id}` — `[IgnoreAntiforgeryToken]` PageModel: `OnGet` / `OnGetOrderBook` / `OnPostBuy` / `OnPostSell` / `OnPostWait` |
| `Pages/Shared/_GameContainer.cshtml` | 全パネルを内包する `<div id="game">` 部分ビュー。Buy/Sell/Wait の `hx-target` |
| `Pages/Shared/_OrderBookPanel.cshtml` | 注文板単独の部分ビュー。ページング `hx-target` |
| `Pages/Shared/OrderBookPanelViewModel.cs` | `(string GameId, OrderBookResponse Book)` |
| `wwwroot/htmx.min.js` + `site.css` | htmx 2.x ローカルコピーと最小スタイル |

### Endpoints

| Method | Path | Action |
|---|---|---|
| POST | `/api/games` | `GameStore.CreateGame` → 201 Created |
| GET | `/api/games/{id}` | `GameStore.GetGame` → 200 / 404 |
| POST | `/api/games/{id}/orders` | `TurnProcessor.Buy`/`Sell` (body の `side` で分岐) via `ProcessOrder` → 200 / 400 / 404 |
| POST | `/api/games/{id}/wait` | `TurnProcessor.Wait` → 200 / 404 |
| GET  | `/` | ナビゲーション HTML（HTMX 版・React 版選択） |
| GET  | `/play` | HTMX ホーム |
| POST | `/play` | ゲーム作成 → 302 to `/play/{id}` |
| GET  | `/play/{id}` | `_GameContainer` 描画（`#orderbook` は `hx-trigger="load"` で遅延ロード） |
| POST | `/play/{id}?handler=Buy\|Sell\|Wait` | `TurnProcessor` 呼び出し → `_GameContainer` 部分ビュー（200 / 400 / 404） |
| GET  | `/play/{id}?handler=OrderBook&page=N` | `_OrderBookPanel` 部分ビュー（200 / 400 / 404） |

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
- **HTMX フロントエンド (`Pages/`)**: API と同一プロセスで Razor Pages による `/play` 系画面を提供。React 版 (`frontend/`, `localhost:5173`) と並走可能で、`/api/...` エンドポイントは共有する。`GameModel` は `[IgnoreAntiforgeryToken]` — htmx POST は antiforgery トークンを自動挿入できないためプロト段階で無効化（CSRF リスクは CORS 開放と同等）。Buy/Sell/Wait は `_GameContainer` 部分ビューを返し `#game` を `outerHTML` swap、注文板ページングは `_OrderBookPanel` 部分ビューを返し `#orderbook` のみ更新。
- **Serilog `preserveStaticLogger: true`**: `WebApplicationFactory<Program>` を複数のテストクラス（`GameApiTests` + `HtmxPagesTests`）が立ち上げると `ReloadableLogger.Freeze()` が衝突するため設定。プロダクションコードは `ILogger<T>` を DI で解決する必要がある — `Serilog.Log.*` 直接呼び出しは bootstrap console シンクのみで、`OrderLog` のローリングファイルシンクには届かない。
