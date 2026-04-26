# OrderBook API ページング対応 実装計画

**Goal:** `/api/admin/games/{id}/orderbook` にクエリパラメータでページングを追加し、大量注文でも扱いやすくする

**Architecture:** クエリパラメータ `page` (1始まり, default=1) と `pageSize` (default=50, max=200) を受け取り、`OrderBookResponse` に `totalCount` / `page` / `pageSize` を追加する。ドメイン (`OrderBook`) は変更しない — API 層 (Mapper + Endpoint) のみ修正。無効値は 400 Bad Request。

**Tech Stack:** ASP.NET Core Minimal API (.NET 9), xUnit, `WebApplicationFactory<Program>` 統合テスト

---

## File Structure

- **Modify** `src/FinLearn.Api/Dtos/OrderBookResponse.cs` — DTO にページング用フィールド追加
- **Modify** `src/FinLearn.Api/Mappers/OrderBookMapper.cs` — `ToResponse` にページング引数を追加
- **Modify** `src/FinLearn.Api/Endpoints/AdminEndpoints.cs` — クエリパラメータ受け取り + バリデーション
- **Modify** `tests/FinLearn.Api.Tests/GameApiTests.cs` — 既存テストの型更新 + ページングテスト追加
- **Modify** `docs/API.md` — エンドポイント仕様更新

---

## Task 1: DTO にページング情報を追加

**Files:**
- Modify: `src/FinLearn.Api/Dtos/OrderBookResponse.cs`

- [ ] **Step 1: DTO を拡張する**

ファイルを以下の内容に置き換える:

```csharp
namespace FinLearn.Api.Dtos;

public sealed record OrderBookResponse(
    IReadOnlyList<OrderDto> Orders,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record OrderDto(
    int Id,
    string TraderId,
    int InstrumentId,
    string Side,
    string Type,
    int Quantity,
    int? Price,
    int? StopPrice,
    int CreatedAtTurn);
```

- [ ] **Step 2: ビルドして DTO 変更の影響範囲を確認**

Run: `dotnet build`
Expected: `OrderBookMapper.ToResponse` の `new OrderBookResponse(orders)` でコンパイルエラー（引数不足）。次タスクで修正する。

- [ ] **Step 3: コミット（コンパイル通過後、Task 2 完了時に一緒にコミット）**

このタスク単体ではコミットしない（Task 2 で Mapper を直すまでビルドが壊れるため）。

---

## Task 2: Mapper にページング引数を追加

**Files:**
- Modify: `src/FinLearn.Api/Mappers/OrderBookMapper.cs`

- [ ] **Step 1: Mapper を書き換える**

ファイルを以下の内容に置き換える:

```csharp
using FinLearn.Api.Dtos;
using FinLearn.Core;

namespace FinLearn.Api.Mappers;

public static class OrderBookMapper
{
    public static OrderBookResponse ToResponse(OrderBook book, int page, int pageSize)
    {
        var all = book.Orders;
        var totalCount = all.Count;

        var skip = (page - 1) * pageSize;
        var pagedOrders = all
            .Skip(skip)
            .Take(pageSize)
            .Select(o => new OrderDto(
                Id: o.Id,
                TraderId: o.TraderId,
                InstrumentId: o.Instrument.Id,
                Side: o.Side.ToString(),
                Type: o.Type.ToString(),
                Quantity: o.Quantity,
                Price: o.Price,
                StopPrice: o.StopPrice,
                CreatedAtTurn: o.CreatedAtTurn
            ))
            .ToList();

        return new OrderBookResponse(pagedOrders, totalCount, page, pageSize);
    }
}
```

- [ ] **Step 2: ビルドして Endpoints の呼び出し箇所のエラーを確認**

Run: `dotnet build`
Expected: `AdminEndpoints.cs` で `OrderBookMapper.ToResponse(game.OrderBook)` が引数不足エラー。次タスクで修正。

---

## Task 3: Endpoint にクエリパラメータとバリデーションを実装（テスト先行）

**Files:**
- Modify: `tests/FinLearn.Api.Tests/GameApiTests.cs`
- Modify: `src/FinLearn.Api/Endpoints/AdminEndpoints.cs`

### 設計（全タスク共通）

- クエリパラメータ:
  - `page` (int, default=1) — 1始まり
  - `pageSize` (int, default=50, max=200)
- バリデーション:
  - `page < 1` → 400 Bad Request `"page must be >= 1"`
  - `pageSize < 1` → 400 Bad Request `"pageSize must be >= 1"`
  - `pageSize > 200` → 400 Bad Request `"pageSize must be <= 200"`
- `page` がデータ範囲超過 → 200 OK + 空配列（`totalCount` は全件数）

- [ ] **Step 1: 既存テストはそのまま、ページングテスト 6 件を追加**

既存 3 テスト（`GET_admin_orderbook_注文実行後に未約定注文が返る` / `GET_admin_orderbook_存在しないゲームは404` / `GET_admin_orderbook_新規ゲームでは空リスト`）はアサーションが `Orders` プロパティのみを参照しているため、DTO 拡張後も無修正で PASS する。差し替え不要。

`tests/FinLearn.Api.Tests/GameApiTests.cs` の `GET_admin_orderbook_新規ゲームでは空リスト` の閉じ括弧 `}` の次の行に、以下のページングテスト 6 つを追加する:

```csharp
    [Fact]
    public async Task GET_admin_orderbook_ページングパラメータ無指定ではdefaultが返る()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderBook = await response.Content.ReadFromJsonAsync<OrderBookResponse>();
        Assert.NotNull(orderBook);
        Assert.Equal(1, orderBook.Page);
        Assert.Equal(50, orderBook.PageSize);
        Assert.Equal(0, orderBook.TotalCount);
    }

    [Fact]
    public async Task GET_admin_orderbook_pageSize1でorderが1件ずつ取得できる()
    {
        var created = await CreateGame();

        // 約定しない注文を 3 つ積む（価格 1 での買い指値は約定しにくい）
        for (int i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync(
                $"/api/games/{created.GameId}/buy",
                new OrderRequest(InstrumentId: 1, Quantity: 1, Price: 1));
        }

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook?page=1&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderBook = await response.Content.ReadFromJsonAsync<OrderBookResponse>();
        Assert.NotNull(orderBook);
        Assert.Single(orderBook.Orders);
        Assert.Equal(1, orderBook.Page);
        Assert.Equal(1, orderBook.PageSize);
        Assert.True(orderBook.TotalCount >= 1);
    }

    [Fact]
    public async Task GET_admin_orderbook_range超えのpageは空配列を返す()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook?page=999&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderBook = await response.Content.ReadFromJsonAsync<OrderBookResponse>();
        Assert.NotNull(orderBook);
        Assert.Empty(orderBook.Orders);
        Assert.Equal(999, orderBook.Page);
        Assert.Equal(10, orderBook.PageSize);
    }

    [Fact]
    public async Task GET_admin_orderbook_page0は400()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook?page=0&pageSize=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_admin_orderbook_pageSize0は400()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook?page=1&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GET_admin_orderbook_pageSize201は400()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook?page=1&pageSize=201");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
```


- [ ] **Step 2: テストを実行して FAIL を確認**

Run: `dotnet test tests/FinLearn.Api.Tests/FinLearn.Api.Tests.csproj`
Expected: `AdminEndpoints.cs` がまだ引数不足のためビルドエラー（コンパイルできない状態）。

- [ ] **Step 3: Endpoint を実装する**

`src/FinLearn.Api/Endpoints/AdminEndpoints.cs` を以下の内容に置き換える:

```csharp
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;

namespace FinLearn.Api.Endpoints;

public static class AdminEndpoints
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public static RouteGroupBuilder MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/games/{id}/orderbook", GetOrderBook);

        return group;
    }

    private static IResult GetOrderBook(
        string id,
        GameStore store,
        int? page,
        int? pageSize)
    {
        var pageValue = page ?? 1;
        var pageSizeValue = pageSize ?? DefaultPageSize;

        if (pageValue < 1)
            return Results.BadRequest("page must be >= 1");
        if (pageSizeValue < 1)
            return Results.BadRequest("pageSize must be >= 1");
        if (pageSizeValue > MaxPageSize)
            return Results.BadRequest($"pageSize must be <= {MaxPageSize}");

        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();

        return Results.Ok(OrderBookMapper.ToResponse(game.OrderBook, pageValue, pageSizeValue));
    }
}
```

- [ ] **Step 4: テストを実行して PASS を確認**

Run: `dotnet test tests/FinLearn.Api.Tests/FinLearn.Api.Tests.csproj`
Expected: 全テスト PASS（既存 3 件 + 新規 6 件 + 他の API テスト）。

- [ ] **Step 5: Core のテストが壊れていないことを確認**

Run: `dotnet test`
Expected: 全テスト PASS。

- [ ] **Step 6: コミット**

```bash
git add src/FinLearn.Api/Dtos/OrderBookResponse.cs src/FinLearn.Api/Mappers/OrderBookMapper.cs src/FinLearn.Api/Endpoints/AdminEndpoints.cs tests/FinLearn.Api.Tests/GameApiTests.cs
git commit -m "feat(api): paginate /api/admin/games/{id}/orderbook"
```

---

## Task 4: API ドキュメントを更新

**Files:**
- Modify: `docs/API.md`

- [ ] **Step 1: `write-docs` スキルを起動**

プロジェクト規約: ドキュメント更新時は `write-docs` スキルを使う（`.claude/CLAUDE.md`）。

- [ ] **Step 2: `GET /api/admin/games/{id}/orderbook` セクションを更新**

`docs/API.md` の該当セクション（行 138–162 付近）を以下に置き換える:

```markdown
### GET /api/admin/games/{id}/orderbook

ゲームの注文帳（`OrderBook`）状態を取得する。デバッグ・管理用途。

**クエリパラメータ:**

| 名前 | 型 | デフォルト | 備考 |
|---|---|---|---|
| page | int | 1 | 1始まり。`<1` は 400 |
| pageSize | int | 50 | 1–200。範囲外は 400 |

**レスポンス:** `200 OK`

\`\`\`json
{
  "orders": [
    {
      "id": 1,
      "traderId": "player",
      "instrumentId": 1,
      "side": "Buy",
      "type": "Limit",
      "quantity": 5,
      "price": 100,
      "stopPrice": null,
      "createdAtTurn": 1
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
\`\`\`

`page` がデータ範囲を超えた場合、`orders` は空配列、`totalCount` には全件数が入る。

**エラー:**
- `400 Bad Request`（`page < 1` / `pageSize < 1` / `pageSize > 200`）
- `404 Not Found`（ゲームが存在しない場合）
```

（実際の書き込みでは上記コードブロック内の `\`\`\`` はエスケープされていない ` ``` ` として書くこと。）

- [ ] **Step 3: `OrderBookResponse / OrderDto` DTO セクションも更新**

`docs/API.md` の DTO セクション（行 217–232 付近）の `OrderBookResponse / OrderDto` 部分を以下に置き換える:

```markdown
### OrderBookResponse / OrderDto

\`\`\`
orders      : OrderDto[]
totalCount  : int   # 全注文件数（ページング前）
page        : int   # 現在のページ番号（1始まり）
pageSize    : int   # 1ページあたりの最大件数

OrderDto:
  id             : int
  traderId       : string
  instrumentId   : int
  side           : string       # "Buy" | "Sell"
  type           : string       # "Market" | "Limit" | "Stop" | "StopLimit"
  quantity       : int
  price          : int?
  stopPrice      : int?
  createdAtTurn  : int
\`\`\`
```

- [ ] **Step 4: コミット**

```bash
git add docs/API.md
git commit -m "docs(api): document orderbook pagination"
```

---

## 完了チェック

- [ ] `dotnet build` がエラーなし
- [ ] `dotnet test` が全 PASS
- [ ] `docs/API.md` の OrderBook セクションがページング仕様を反映
- [ ] 2 つのコミットが作成されている（実装 + ドキュメント）
