---
paths:
  - "src/FinLearn.Api/Pages/**"
  - "src/FinLearn.Api/wwwroot/**"
  - "tests/FinLearn.Api.Tests/HtmxPagesTests.cs"
---

## FinLearn.Api/Pages — HTMX Frontend (Razor Pages)

`/play` 配下の Razor Pages + HTMX 2.x によるサーバー駆動フロントエンド。React 版（`frontend/`、`localhost:5173`）と同一プロセスで並走し、JSON API (`/api/...`) と DI コンテナを共有する。画面構成は React 版と同一パネル群を Razor 部分ビューで再構成したもの。詳細仕様は [docs/FRONT.md](../../../docs/FRONT.md)、構築経緯は [docs/writing-plans/htmx-frontend.md](../../../docs/writing-plans/htmx-frontend.md)。

### ファイル構成

| ファイル | 役割 |
|---|---|
| [Pages/Play/Index.cshtml(.cs)](../../../src/FinLearn.Api/Pages/Play/Index.cshtml) | `/play` ホーム。POST → `GameStore.CreateGame` → 302 to `/play/{id}` |
| [Pages/Play/Game.cshtml(.cs)](../../../src/FinLearn.Api/Pages/Play/Game.cshtml.cs) | `/play/{id}` PageModel。`OnGet` / `OnGetOrderBook` / `OnPostBuy` / `OnPostSell` / `OnPostWait` |
| [Pages/Shared/_GameContainer.cshtml](../../../src/FinLearn.Api/Pages/Shared/_GameContainer.cshtml) | 全パネル内包の `<div id="game">` 部分ビュー。Buy/Sell/Wait の `hx-target` |
| [Pages/Shared/_OrderBookPanel.cshtml](../../../src/FinLearn.Api/Pages/Shared/_OrderBookPanel.cshtml) | 注文板単独の部分ビュー。ページング `hx-target` |
| [Pages/Shared/OrderBookPanelViewModel.cs](../../../src/FinLearn.Api/Pages/Shared/OrderBookPanelViewModel.cs) | `(string GameId, OrderBookResponse Book)` |
| [Pages/Shared/_Layout.cshtml](../../../src/FinLearn.Api/Pages/Shared/_Layout.cshtml) | レイアウト。`htmx.min.js` と `site.css` を読み込み |
| `wwwroot/htmx.min.js` + `site.css` | htmx 2.x ローカルコピーと最小スタイル（CDN は使わない） |

### HTMX swap 戦略

- **Buy/Sell/Wait** → `_GameContainer` を返し `#game` を `outerHTML` swap（注文板以外を一括更新）
- **注文板ページング** → `_OrderBookPanel` を返し `#orderbook` を `outerHTML` swap（板のみ更新）
- **初期ロード** → `OnGet` は `_GameContainer` ＋ `<div id="orderbook" hx-trigger="load">` を返し、注文板は別 GET で遅延ロード（DTO 構築コストの分離）

### 設計上の注意

- **`[IgnoreAntiforgeryToken]`**: htmx の `hx-post` は `<form>` の antiforgery トークンを自動挿入できないためプロト段階で無効化。CSRF リスクは CORS で `localhost:5173` を開放した JSON API と同等。
- **エラー応答二系統**（JSON API と同方針）: 形式不正（`Quantity <= 0`、`Price <= 0`）→ 400 BadRequest。ゲーム状態依存の失敗（保有不足等）→ 200 OK ＋ `_GameContainer` に `Model.Warning` を埋め込んで返す（`GameStore` は更新しない）。
- **DTO 構築の共通化**: `BuildResponse` で `IExchangeFactory.Create(prices, fee)` → `GameMapper.ToResponse` を経由する。React 版とロジックを共有するため Razor 側で独自整形しない。
- **`Pages/` 経路でも OrderLog を出す**: `ILogger<OrderLog>` を DI で受け、`OrdersSubmitted` / `OrdersMatched` を JSON API と同形式でログ出力する。`Serilog.Log.*` 直接呼び出しは bootstrap シンクのみで `OrderLog` のローリングファイルには届かない。
- **テスト**: [HtmxPagesTests.cs](../../../tests/FinLearn.Api.Tests/HtmxPagesTests.cs) が `WebApplicationFactory<Program>` で `/play` を叩く。`GameApiTests` と並走するため `Program.cs` の Serilog は `preserveStaticLogger: true`。
