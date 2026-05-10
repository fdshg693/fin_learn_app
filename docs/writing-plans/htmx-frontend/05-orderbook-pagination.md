# Task 5: 注文板ページング

[← Back to plan](../htmx-frontend.md)

`_OrderBookPanel.cshtml` を実データ表示に差し替え、前へ／次へボタンを `hx-get` で配線する。`Game.cshtml.cs` に `OnGetOrderBook(int page)` ハンドラを追加し、`_OrderBookPanel` 単体の部分ビューだけを返す（`_GameContainer` は再描画しない）。`OrderBookMapper.ToResponse` を流用する。

**Files:**
- Modify: `src/FinLearn.Api/Pages/Shared/_OrderBookPanel.cshtml`
- Modify: `src/FinLearn.Api/Pages/Play/Game.cshtml.cs`
- Modify: `src/FinLearn.Api/Pages/Shared/_GameContainer.cshtml`
- Modify: `src/FinLearn.Api/Pages/Shared/OrderBookPanelViewModel.cs`（Task 3 で作成済み、本タスクで構造を変更）
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`

---

- [ ] **Step 1: 失敗テスト追加**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` に追加:

```csharp
    [Fact]
    public async Task GET_play_id_orderbook_は注文板パネルだけのHTMLを返す()
    {
        var gameId = await CreateGameViaApi();

        var response = await _client.GetAsync($"/play/{gameId}?handler=OrderBook&page=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"orderbook\"", body);
        Assert.DoesNotContain("id=\"game\"", body);          // コンテナ全体は返らない
        Assert.DoesNotContain("注文入力", body);              // 他パネルは含まない
    }

    [Fact]
    public async Task GET_play_id_orderbook_pageは1未満で400()
    {
        var gameId = await CreateGameViaApi();

        var response = await _client.GetAsync($"/play/{gameId}?handler=OrderBook&page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
```

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 新規 2 件 FAIL

- [ ] **Step 3: ViewModel の構造を実データ用に置換**

Task 3 で作った `src/FinLearn.Api/Pages/Shared/OrderBookPanelViewModel.cs` を以下に置換:

```csharp
using FinLearn.Api.Dtos;

namespace FinLearn.Api.Pages.Shared;

public sealed record OrderBookPanelViewModel(
    string GameId,
    OrderBookResponse Book);
```

`(string GameId, int Page, int PageSize)` から `(string GameId, OrderBookResponse Book)` への変更。古いシグネチャの参照は本タスクの後続ステップ・Task 4 のコンテナ末尾を全て差し替えるので、ビルドエラーが残らないことをこの後の Step で保証する。

- [ ] **Step 4: `Game.cshtml.cs` に `OnGetOrderBook` ハンドラを追加**

`AdminConfig` を DI 注入し、新ハンドラを追加する。

クラスフィールドとコンストラクタを更新:

```csharp
    private readonly AdminConfig _admin;

    public GameModel(
        GameStore store,
        TurnProcessor processor,
        IExchangeFactory exchangeFactory,
        GameConfig config,
        AdminConfig admin,
        ILogger<OrderLog> logger)
    {
        _store = store;
        _processor = processor;
        _exchangeFactory = exchangeFactory;
        _config = config;
        _admin = admin;
        _logger = logger;
    }
```

ハンドラを追加:

```csharp
    public IActionResult OnGetOrderBook(string id, int page = 1)
    {
        if (page < 1) return BadRequest("page must be >= 1");

        var game = _store.GetGame(id);
        if (game is null) return NotFound();

        var book = FinLearn.Api.Mappers.OrderBookMapper.ToResponse(
            game.OrderBook, page, _admin.DefaultPageSize);
        return Partial("_OrderBookPanel",
            new FinLearn.Api.Pages.Shared.OrderBookPanelViewModel(id, book));
    }
```

注: `OnGet` / `HandleTrade` / `OnPostWait` 側に `OrderBook` 構築は不要。Step 6 で `_GameContainer` 末尾を `hx-trigger="load"` の自動取得 div に置き換えるため、初期表示や POST 後の再描画でも注文板パネルは別経路（`OnGetOrderBook`）から取得される。

- [ ] **Step 5: `_OrderBookPanel.cshtml` を実装**

`src/FinLearn.Api/Pages/Shared/_OrderBookPanel.cshtml` を以下に置換:

```cshtml
@model FinLearn.Api.Pages.Shared.OrderBookPanelViewModel
@{
    var book = Model.Book;
    var totalPages = Math.Max(1, (int)Math.Ceiling(book.TotalCount / (double)book.PageSize));
    var hasPrev = book.Page > 1;
    var hasNext = book.Page < totalPages;
    var rangeStart = book.TotalCount == 0 ? 0 : (book.Page - 1) * book.PageSize + 1;
    var rangeEnd = Math.Min(book.TotalCount, book.Page * book.PageSize);
}
<section id="orderbook">
    <h2>注文板</h2>
    @if (book.TotalCount == 0)
    {
        <p>注文なし</p>
    }
    else
    {
        <table>
            <thead>
                <tr><th>ID</th><th>トレーダー</th><th>銘柄</th><th>売買</th><th>種類</th><th>数量</th><th>価格</th></tr>
            </thead>
            <tbody>
            @foreach (var o in book.Orders)
            {
                <tr>
                    <td>@o.Id</td>
                    <td>@o.TraderId</td>
                    <td>@o.InstrumentId</td>
                    <td class="@(o.Side == "Buy" ? "buy" : "sell")">@o.Side</td>
                    <td>@o.Type</td>
                    <td>@o.Quantity</td>
                    <td>@(o.Price?.ToString() ?? "-")</td>
                </tr>
            }
            </tbody>
        </table>
        <p>@rangeStart–@rangeEnd / @book.TotalCount</p>
        <button
            hx-get="/play/@Model.GameId?handler=OrderBook&page=@(book.Page - 1)"
            hx-target="#orderbook"
            hx-swap="outerHTML"
            @(hasPrev ? "" : "disabled")>前へ</button>
        <button
            hx-get="/play/@Model.GameId?handler=OrderBook&page=@(book.Page + 1)"
            hx-target="#orderbook"
            hx-swap="outerHTML"
            @(hasNext ? "" : "disabled")>次へ</button>
    }
</section>
```

- [ ] **Step 6: `_GameContainer.cshtml` から `_OrderBookPanel` 直接呼び出しを撤去し、自動ロード div に置き換える**

`_GameContainer.cshtml` 末尾の `<partial name="_OrderBookPanel" ... />` 行を、`hx-trigger="load"` の自動取得 div 1 行に置換する:

```cshtml
    <div id="orderbook"
         hx-get="/play/@Model.GameId?handler=OrderBook&page=1"
         hx-trigger="load"
         hx-swap="outerHTML"></div>
</div>
```

理由: `_GameContainer` は `GameResponse` だけをモデルに持つ。注文板は別経路で取得した方が、コンテナ全体差し替え（初期 GET / Buy / Sell / Wait）と注文板単体差し替え（前へ／次へ）を同じ部分ビュー名で扱える。`hx-trigger="load"` でブラウザは描画直後に 1 回だけ `OnGetOrderBook` を呼び、レスポンスで `#orderbook` を `outerHTML` 置換する。POST 後にコンテナごと差し替えられた場合も、新しい `<div id="orderbook" hx-trigger="load">` が再評価されて再ロードされる。

`Game.cshtml` は変更不要（Task 3 のまま）。

- [ ] **Step 7: テスト確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 全件 PASS

注: Task 3 で追加した `GET_play_id_でゲーム画面の全パネル見出しを含むHTMLを返す` テストの `Assert.Contains("注文板", body)` は、`hx-trigger="load"` の遅延ロードに切り替えた結果、初回 HTML には「注文板」見出しが含まれない。テストを以下に修正する:

```csharp
        Assert.Contains("id=\"orderbook\"", body);   // 「注文板」見出しは hx-trigger=load で後追い注入
```

- [ ] **Step 8: 全テスト確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 9: 手動動作確認**

```powershell
dotnet run --project src/FinLearn.Api
```

ブラウザで `/play` からゲームを始め、買い注文 → 注文板に板が乗り、ページ送りで先頭・最終ページの「前へ」「次へ」が disabled になることを確認。

- [ ] **Step 10: コミット**

```powershell
git add src/FinLearn.Api/Pages tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): order book panel with hx-get pagination"
```
