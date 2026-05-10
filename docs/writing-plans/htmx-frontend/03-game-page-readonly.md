# Task 3: ゲーム画面 — 読み取り専用レンダリング

[← Back to plan](../htmx-frontend.md)

`/play/{id}` で `_GameContainer` 部分ビューに全 9 パネルを描画する。アクションは未配線（次タスク）。`GameMapper.ToResponse` を呼んで `GameResponse` をそのまま View Model に渡す。`OrderBookPanel` だけは独立した部分ビュー（`_OrderBookPanel.cshtml`）に切り出し、Task 5 のページング対応に備える。

**Files:**
- Create: `src/FinLearn.Api/Pages/Play/Game.cshtml`
- Create: `src/FinLearn.Api/Pages/Play/Game.cshtml.cs`
- Create: `src/FinLearn.Api/Pages/Shared/_GameContainer.cshtml`
- Create: `src/FinLearn.Api/Pages/Shared/_OrderBookPanel.cshtml`
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`

---

- [ ] **Step 1: 失敗テスト追加**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` に追加:

```csharp
    [Fact]
    public async Task GET_play_id_でゲーム画面の全パネル見出しを含むHTMLを返す()
    {
        var create = await _client.PostAsync("/api/games", null);
        var created = await create.Content.ReadFromJsonAsync<FinLearn.Api.Dtos.GameResponse>();

        var response = await _client.GetAsync($"/play/{created!.GameId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"game\"", body);
        Assert.Contains("ターン", body);                  // GameHeader
        Assert.Contains("現金", body);                    // PlayerPanel
        Assert.Contains("未約定注文", body);              // PendingOrders
        Assert.Contains("銘柄一覧", body);                // MarketBoard
        Assert.Contains("保有ポジション", body);          // PositionList
        Assert.Contains("注文入力", body);                // TradeForm
        Assert.Contains("注文板", body);                  // OrderBookPanel
        // TradeHistory は約定がまだ無いので非表示で OK
    }

    [Fact]
    public async Task GET_play_id_存在しないゲームは404()
    {
        var response = await _client.GetAsync("/play/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
```

`using System.Net.Http.Json;` をファイル先頭に追加（既存テストに無ければ）。

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 新規 2 件 FAIL（`/play/{id}` が 404）

- [ ] **Step 3: ゲーム画面 PageModel を作成**

`src/FinLearn.Api/Pages/Play/Game.cshtml.cs`:

```csharp
using FinLearn.Api.Dtos;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class GameModel : PageModel
{
    private readonly GameStore _store;
    private readonly IExchangeFactory _exchangeFactory;
    private readonly GameConfig _config;

    public GameModel(GameStore store, IExchangeFactory exchangeFactory, GameConfig config)
    {
        _store = store;
        _exchangeFactory = exchangeFactory;
        _config = config;
    }

    public GameResponse Game { get; private set; } = default!;

    public IActionResult OnGet(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();
        var exchange = _exchangeFactory.Create(game.Prices, _config.Fee);
        var recentTrades = _store.GetRecentTrades(id);
        Game = GameMapper.ToResponse(id, game, exchange, recentTrades: recentTrades);
        return Page();
    }
}
```

- [ ] **Step 4: ゲーム画面 View を作成**

`src/FinLearn.Api/Pages/Play/Game.cshtml`:

```cshtml
@page "/play/{id}"
@model FinLearn.Api.Pages.Play.GameModel
@{
    ViewData["Title"] = $"ゲーム {Model.Game.GameId}";
}
<partial name="_GameContainer" model="Model.Game" />
```

- [ ] **Step 5: `_GameContainer.cshtml` を作成**

`src/FinLearn.Api/Pages/Shared/_GameContainer.cshtml`:

```cshtml
@model FinLearn.Api.Dtos.GameResponse
<div id="game">
    <header>
        <h1>ターン @Model.Turn — @Model.Player.Name</h1>
    </header>

    @if (!string.IsNullOrEmpty(Model.Warning))
    {
        <div class="warning" role="alert">@Model.Warning</div>
    }

    <section>
        <h2>プレイヤー情報</h2>
        <dl>
            <dt>現金</dt><dd>@Model.Player.Cash JPY</dd>
            <dt>総資産</dt><dd>@Model.Player.TotalAssets JPY</dd>
            <dt>損益</dt>
            <dd class="@(Model.Player.ProfitLoss >= 0 ? "profit" : "loss")">@Model.Player.ProfitLoss JPY</dd>
        </dl>
    </section>

    <section>
        <h2>未約定注文</h2>
        @if (Model.Player.PendingOrders.Count == 0)
        {
            <p>未約定注文はありません</p>
        }
        else
        {
            <table>
                <thead>
                    <tr><th>売買</th><th>銘柄</th><th>種類</th><th>数量</th><th>価格</th><th>残ターン</th></tr>
                </thead>
                <tbody>
                @foreach (var o in Model.Player.PendingOrders)
                {
                    <tr>
                        <td class="@(o.Side == "Buy" ? "buy" : "sell")">@o.Side</td>
                        <td>@o.InstrumentId</td>
                        <td>@o.Type</td>
                        <td>@o.Quantity</td>
                        <td>@(o.Price?.ToString() ?? "-")</td>
                        <td>@(Math.Max(0, o.ExpiresAtTurn - Model.Turn))</td>
                    </tr>
                }
                </tbody>
            </table>
        }
    </section>

    <section>
        <h2>銘柄一覧</h2>
        <table>
            <thead><tr><th>銘柄ID</th><th>現在価格</th></tr></thead>
            <tbody>
            @foreach (var i in Model.Instruments)
            {
                <tr><td>@i.Id</td><td>@i.Price</td></tr>
            }
            </tbody>
        </table>
    </section>

    <section>
        <h2>保有ポジション</h2>
        @if (Model.Player.Positions.Count == 0)
        {
            <p>保有ポジションはありません</p>
        }
        else
        {
            <table>
                <thead><tr><th>銘柄ID</th><th>数量</th><th>現在価格</th><th>評価額</th></tr></thead>
                <tbody>
                @foreach (var p in Model.Player.Positions)
                {
                    <tr><td>@p.InstrumentId</td><td>@p.Quantity</td><td>@p.CurrentPrice</td><td>@p.Amount</td></tr>
                }
                </tbody>
            </table>
        }
    </section>

    <section>
        <h2>注文入力</h2>
        <form>
            <label>銘柄
                <select name="instrumentId">
                    @foreach (var i in Model.Instruments)
                    {
                        <option value="@i.Id">@i.Id</option>
                    }
                </select>
            </label>
            <label>数量 <input type="number" name="quantity" min="1" value="1" /></label>
            <label>価格 <input type="number" name="price" min="1" placeholder="空欄=成行" /></label>
            <button type="button" disabled>買う</button>
            <button type="button" disabled>売る</button>
            <button type="button" disabled>待つ</button>
        </form>
    </section>

    @if (Model.RecentTrades.Count > 0)
    {
        <section>
            <h2>最近の約定</h2>
            <table>
                <thead><tr><th>売買</th><th>銘柄</th><th>数量</th><th>金額</th><th>手数料</th></tr></thead>
                <tbody>
                @foreach (var t in Model.RecentTrades.Reverse())
                {
                    <tr>
                        <td class="@(t.Side == "Buy" ? "buy" : "sell")">@t.Side</td>
                        <td>@t.InstrumentId</td>
                        <td>@t.FilledQuantity</td>
                        <td>@t.TotalAmount</td>
                        <td>@t.Fee</td>
                    </tr>
                }
                </tbody>
            </table>
        </section>
    }

    <partial name="_OrderBookPanel"
             model="@(new FinLearn.Api.Pages.Shared.OrderBookPanelViewModel(Model.GameId, 1, 20))" />
</div>
```

注: ボタンは `disabled` のまま（次タスクで `hx-post` を追加して有効化する）。

- [ ] **Step 6: 部分ビューが受け取る ViewModel を C# クラスとして定義**

`src/FinLearn.Api/Pages/Shared/OrderBookPanelViewModel.cs`:

```csharp
namespace FinLearn.Api.Pages.Shared;

public sealed record OrderBookPanelViewModel(string GameId, int Page, int PageSize);
```

注: Task 5 でフィールド構成を `(string GameId, OrderBookResponse Book)` に置き換えるが、本タスクではダミー描画用の最小形でよい。

- [ ] **Step 7: `_OrderBookPanel.cshtml` を作成**

`src/FinLearn.Api/Pages/Shared/_OrderBookPanel.cshtml`:

```cshtml
@model FinLearn.Api.Pages.Shared.OrderBookPanelViewModel
<section id="orderbook">
    <h2>注文板</h2>
    <p>ページ @Model.Page（実装は次タスク）</p>
</section>
```

注: 現時点ではダミー描画。Task 5 で `OrderBookMapper.ToResponse` を呼んで実データ表示に差し替える。

- [ ] **Step 8: ビルド確認**

Run: `dotnet build src/FinLearn.Api`
Expected: 成功。

- [ ] **Step 9: テスト確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 全件 PASS（既存 + 新規 2 件）

- [ ] **Step 10: 全テスト確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 11: 手動動作確認**

```powershell
dotnet run --project src/FinLearn.Api
```

別ターミナルで:

```powershell
$r = Invoke-WebRequest -Method Post -Uri http://localhost:5088/api/games
$id = ($r.Content | ConvertFrom-Json).gameId
Start-Process "http://localhost:5088/play/$id"
```

Expected: ブラウザで全パネル見出しが表示され、ボタンは disabled。サーバを Ctrl+C で停止。

- [ ] **Step 12: コミット**

```powershell
git add src/FinLearn.Api/Pages tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): /play/{id} read-only render with all panels"
```
