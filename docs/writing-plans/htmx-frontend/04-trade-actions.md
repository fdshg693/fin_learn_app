# Task 4: 取引アクション — Buy/Sell/Wait

[← Back to plan](../htmx-frontend.md)

`_TradeForm` の 3 ボタンを `hx-post` で配線し、`Game.cshtml.cs` に `OnPostBuyAsync` / `OnPostSellAsync` / `OnPostWaitAsync` ハンドラを実装。各ハンドラは `TurnProcessor` を呼び、更新後の `_GameContainer` 部分ビューだけを返す。`hx-target="#game" hx-swap="outerHTML"` でクライアント側はコンテナ全体を差し替える。形式不正は `BadRequest` を返し、状態依存失敗（残高不足など）は `warning` フィールド経由で `_GameContainer` 内に表示される。

**Files:**
- Modify: `src/FinLearn.Api/Pages/Play/Game.cshtml.cs`
- Modify: `src/FinLearn.Api/Pages/Shared/_GameContainer.cshtml`
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`

---

- [ ] **Step 1: 失敗テスト追加**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` に追加:

```csharp
    [Fact]
    public async Task POST_play_id_buy_は更新済みコンテナHTMLを返す()
    {
        var gameId = await CreateGameViaApi();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["instrumentId"] = "1",
            ["quantity"] = "1",
        });
        var response = await _client.PostAsync($"/play/{gameId}?handler=Buy", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"game\"", body);
        Assert.Contains("ターン 2", body); // Buy で 1 ターン進む
    }

    [Fact]
    public async Task POST_play_id_wait_は更新済みコンテナHTMLを返す()
    {
        var gameId = await CreateGameViaApi();

        var response = await _client.PostAsync($"/play/{gameId}?handler=Wait",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ターン 2", body);
    }

    [Fact]
    public async Task POST_play_id_buy_quantity0は400()
    {
        var gameId = await CreateGameViaApi();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["instrumentId"] = "1",
            ["quantity"] = "0",
        });
        var response = await _client.PostAsync($"/play/{gameId}?handler=Buy", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_play_id_sell_保有なしはwarning付きで200を返す()
    {
        var gameId = await CreateGameViaApi();

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["instrumentId"] = "1",
            ["quantity"] = "10",
        });
        var response = await _client.PostAsync($"/play/{gameId}?handler=Sell", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("class=\"warning\"", body);
    }

    private async Task<string> CreateGameViaApi()
    {
        var create = await _client.PostAsync("/api/games", null);
        var created = await create.Content.ReadFromJsonAsync<FinLearn.Api.Dtos.GameResponse>();
        return created!.GameId;
    }
```

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 新規 4 件 FAIL（ハンドラ未実装）

- [ ] **Step 3: PageModel にハンドラを追加**

`src/FinLearn.Api/Pages/Play/Game.cshtml.cs` を以下に置換:

```csharp
using FinLearn.Api.Dtos;
using FinLearn.Api.Endpoints;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class GameModel : PageModel
{
    private readonly GameStore _store;
    private readonly TurnProcessor _processor;
    private readonly IExchangeFactory _exchangeFactory;
    private readonly GameConfig _config;
    private readonly ILogger<OrderLog> _logger;

    public GameModel(
        GameStore store,
        TurnProcessor processor,
        IExchangeFactory exchangeFactory,
        GameConfig config,
        ILogger<OrderLog> logger)
    {
        _store = store;
        _processor = processor;
        _exchangeFactory = exchangeFactory;
        _config = config;
        _logger = logger;
    }

    public GameResponse Game { get; private set; } = default!;

    [BindProperty(SupportsGet = false)] public int InstrumentId { get; set; }
    [BindProperty(SupportsGet = false)] public int Quantity { get; set; }
    [BindProperty(SupportsGet = false)] public int? Price { get; set; }

    public IActionResult OnGet(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();
        Game = BuildResponse(id, game, warning: null);
        return Page();
    }

    public IActionResult OnPostBuy(string id) => HandleTrade(id, OrderSide.Buy);
    public IActionResult OnPostSell(string id) => HandleTrade(id, OrderSide.Sell);

    public IActionResult OnPostWait(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();

        var turn = _processor.Wait(game, _config.Fee);
        _store.UpdateGame(id, turn.Game);
        LogTurn(id, turn);

        Game = BuildResponse(id, turn.Game, turn.Warning);
        return Partial("_GameContainer", Game);
    }

    private IActionResult HandleTrade(string id, OrderSide side)
    {
        if (Quantity <= 0)
            return BadRequest(new { error = "quantity は 1 以上を指定してください" });
        if (Price is not null && Price <= 0)
            return BadRequest(new { error = "price は 1 以上を指定してください" });

        var game = _store.GetGame(id);
        if (game is null) return NotFound();

        var expiresInTurns = GameRules.DefaultOrderTtl;
        var turn = side == OrderSide.Buy
            ? _processor.Buy(game, _config.Fee, InstrumentId, Quantity, Price, null, expiresInTurns)
            : _processor.Sell(game, _config.Fee, InstrumentId, Quantity, Price, null, expiresInTurns);

        if (turn.Warning is null)
        {
            _store.UpdateGame(id, turn.Game);
            if (turn.Trade is not null && turn.Trade.FilledQuantity > 0)
                _store.AddTrade(id, turn.Trade);
        }
        LogTurn(id, turn);

        Game = BuildResponse(id, turn.Game, turn.Warning);
        return Partial("_GameContainer", Game);
    }

    private GameResponse BuildResponse(string id, Game game, string? warning)
    {
        var exchange = _exchangeFactory.Create(game.Prices, _config.Fee);
        var recentTrades = _store.GetRecentTrades(id);
        return GameMapper.ToResponse(id, game, exchange, warning, recentTrades);
    }

    private void LogTurn(string id, TurnResult result)
    {
        _logger.LogInformation(
            "OrdersSubmitted Game={GameId} Turn={Turn} Count={Count} Warning={Warning} {@Orders}",
            id, result.ProcessedTurn, result.SubmittedOrders.Count,
            result.Warning, result.SubmittedOrders);
        _logger.LogInformation(
            "OrdersMatched Game={GameId} Turn={Turn} Count={Count} {@Fills}",
            id, result.ProcessedTurn, result.Fills.Count, result.Fills);
    }
}
```

- [ ] **Step 4: `_GameContainer.cshtml` の TradeForm を hx-post 化**

`_GameContainer.cshtml` の `<section>` 「注文入力」ブロックを以下に置換:

```cshtml
    <section>
        <h2>注文入力</h2>
        <form id="trade-form" hx-target="#game" hx-swap="outerHTML">
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
            <button type="button" hx-post="/play/@Model.GameId?handler=Buy" hx-include="#trade-form">買う</button>
            <button type="button" hx-post="/play/@Model.GameId?handler=Sell" hx-include="#trade-form">売る</button>
            <button type="button" hx-post="/play/@Model.GameId?handler=Wait">待つ</button>
        </form>
    </section>
```

注: Razor Pages の handler 名は `OnPostBuy` → クエリ `?handler=Buy` で呼ばれる規約。`hx-include` で同一フォーム内の入力を送る。`待つ` は入力不要なので `hx-include` を付けない。

- [ ] **Step 5: antiforgery 対策**

Razor Pages はデフォルトで POST に antiforgery トークンを要求する。HTMX ボタンは `<form>` 要素に hidden トークンが自動挿入されないため、PageModel に `[IgnoreAntiforgeryToken]` 属性を追加する。

`Game.cshtml.cs` のクラス宣言を変更:

```csharp
[IgnoreAntiforgeryToken]
public class GameModel : PageModel
```

理由: HTMX 用 POST はゲーム ID を URL に持つ自分自身向けの操作で、CSRF リスクは元の JSON API（CORS で `localhost:5173` に開放）と同等。antiforgery を要求する場合は別途 hidden 埋め込みが必要になる。プロト段階では `IgnoreAntiforgeryToken` で揃える（README に記載）。

- [ ] **Step 6: テスト確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 全件 PASS

- [ ] **Step 7: 全テスト確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 8: 手動動作確認**

```powershell
dotnet run --project src/FinLearn.Api
```

ブラウザで `/play` → ゲーム開始 → 「買う」「売る」「待つ」が動作し、ターン数が増えることを確認。残高不足の売却で warning が表示されることを確認。

- [ ] **Step 9: コミット**

```powershell
git add src/FinLearn.Api/Pages tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): wire Buy/Sell/Wait via hx-post returning _GameContainer partial"
```
