using System.Net;
using System.Net.Http.Json;
using FinLearn.Api.Dtos;
using FinLearn.Core;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinLearn.Api.Tests;

public class GameApiTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GameApiTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_games_ゲームを作成して初期状態を返す()
    {
        var response = await _client.PostAsync("/api/games", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.False(string.IsNullOrEmpty(game.GameId));
        Assert.Equal(1, game.Turn);
        Assert.Equal("player", game.Player.Name);
        Assert.Equal(10000, game.Player.Cash);
        Assert.Empty(game.Player.Positions);
        Assert.Equal(10000, game.Player.TotalAssets);
        Assert.Equal(0, game.Player.ProfitLoss);
        Assert.Equal(3, game.Instruments.Count);
        Assert.All(game.Instruments, i => Assert.Equal(100, i.Price));
        Assert.Null(game.Warning);
    }

    [Fact]
    public async Task GET_games_id_作成したゲームを取得できる()
    {
        var createResponse = await _client.PostAsync("/api/games", null);
        var created = await createResponse.Content.ReadFromJsonAsync<GameResponse>();

        var response = await _client.GetAsync($"/api/games/{created!.GameId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Equal(created.GameId, game.GameId);
        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public async Task GET_games_id_存在しないゲームは404()
    {
        var response = await _client.GetAsync("/api/games/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task POST_orders_数量が0以下で400(int quantity)
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: quantity));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task POST_orders_priceが0以下で400(int price)
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: price));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task POST_orders_stopPriceが0以下で400(int stopPrice)
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, StopPrice: stopPrice));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task POST_orders_expiresInTurnsが0以下で400(int expiresInTurns)
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1, ExpiresInTurns: expiresInTurns));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_orders_expiresInTurns未指定でデフォルト2が適用される()
    {
        var created = await CreateGame();

        // 約定しない低価格で指値買い → 板に残る。ExpiresAtTurn = 1 + 2 = 3
        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderBook = await _client.GetFromJsonAsync<OrderBookResponse>(
            $"/api/admin/games/{created.GameId}/orderbook");
        Assert.NotNull(orderBook);
        var playerOrder = orderBook.Orders.FirstOrDefault(o => o.TraderId == "player");
        Assert.NotNull(playerOrder);
        Assert.Equal(3, playerOrder.ExpiresAtTurn);
    }

    [Fact]
    public async Task POST_orders_expiresInTurns指定でExpiresAtTurnに反映される()
    {
        var created = await CreateGame();

        // ターン1で expiresInTurns=5 → ExpiresAtTurn = 1 + 5 = 6
        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1, ExpiresInTurns: 5));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var orderBook = await _client.GetFromJsonAsync<OrderBookResponse>(
            $"/api/admin/games/{created.GameId}/orderbook");
        Assert.NotNull(orderBook);
        var playerOrder = orderBook.Orders.FirstOrDefault(o => o.TraderId == "player");
        Assert.NotNull(playerOrder);
        Assert.Equal(6, playerOrder.ExpiresAtTurn);
    }

    [Fact]
    public async Task POST_sell_保有なし売り注文でwarning付きだがターンは進む()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Sell, InstrumentId: 1, Quantity: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.NotNull(game.Warning);
        // コンピューター注文は常に板に残すため、失敗時もターンが進む
        Assert.Equal(2, game.Turn);
    }

    [Fact]
    public async Task POST_sell_存在しないゲームは404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/games/nonexistent/orders",
            new OrderRequest(Side: OrderSide.Sell, InstrumentId: 1, Quantity: 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_buy_存在しないゲームは404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/games/nonexistent/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_wait_ターンが進む()
    {
        var created = await CreateGame();

        var response = await _client.PostAsync($"/api/games/{created.GameId}/wait", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Equal(2, game.Turn);
        Assert.Null(game.Warning);
    }

    [Fact]
    public async Task POST_wait_存在しないゲームは404()
    {
        var response = await _client.PostAsync("/api/games/nonexistent/wait", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_orders_400後はターンも板も不変()
    {
        var created = await CreateGame();

        // 形式不正のリクエストを複数送信
        await _client.PostAsJsonAsync($"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 0));
        await _client.PostAsJsonAsync($"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 0));
        await _client.PostAsJsonAsync($"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, StopPrice: -1));

        // ターン不変
        var game = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{created.GameId}");
        Assert.NotNull(game);
        Assert.Equal(1, game.Turn);

        // コンピューター注文も生成されていない（板が空）
        var orderBook = await _client.GetFromJsonAsync<OrderBookResponse>(
            $"/api/admin/games/{created.GameId}/orderbook");
        Assert.NotNull(orderBook);
        Assert.Empty(orderBook.Orders);
    }

    [Fact]
    public async Task POST_orders_404後も既存ゲームのターンは不変()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            "/api/games/nonexistent/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var game = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{created.GameId}");
        Assert.NotNull(game);
        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public async Task POST_orders_side未指定で400()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new { instrumentId = 1, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_orders_不正なsideで400()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new { side = "Hold", instrumentId = 1, quantity = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task レスポンスのinstrumentsにIDと価格が含まれる()
    {
        var created = await CreateGame();

        Assert.Equal(3, created.Instruments.Count);
        Assert.Contains(created.Instruments, i => i.Id == 1);
        Assert.Contains(created.Instruments, i => i.Id == 2);
        Assert.Contains(created.Instruments, i => i.Id == 3);
    }

    [Fact]
    public async Task GET_admin_orderbook_注文実行後に未約定注文が返る()
    {
        var created = await CreateGame();

        // 指値買い注文（約定しにくい低価格）で板に注文を残す
        await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1));

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderBook = await response.Content.ReadFromJsonAsync<OrderBookResponse>();
        Assert.NotNull(orderBook);
        Assert.NotEmpty(orderBook.Orders);
    }

    [Fact]
    public async Task GET_admin_orderbook_存在しないゲームは404()
    {
        var response = await _client.GetAsync("/api/admin/games/nonexistent/orderbook");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_admin_orderbook_新規ゲームでは空リスト()
    {
        var created = await CreateGame();

        var response = await _client.GetAsync($"/api/admin/games/{created.GameId}/orderbook");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orderBook = await response.Content.ReadFromJsonAsync<OrderBookResponse>();
        Assert.NotNull(orderBook);
        Assert.Empty(orderBook.Orders);
    }

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

        for (int i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync(
                $"/api/games/{created.GameId}/orders",
                new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1));
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

    [Fact]
    public async Task POST_orders_約定ゼロの指値注文はrecentTradesに含まれない()
    {
        var created = await CreateGame();

        // 約定しない低価格指値買い → 板に残るが約定数量0
        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Empty(game.RecentTrades);
    }

    [Fact]
    public async Task POST_orders_未約定の指値注文がpendingOrdersに含まれる()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 5, Price: 1, ExpiresInTurns: 3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        var pending = Assert.Single(game.Player.PendingOrders);
        Assert.Equal(1, pending.InstrumentId);
        Assert.Equal("Buy", pending.Side);
        Assert.Equal("Limit", pending.Type);
        Assert.Equal(5, pending.Quantity);
        Assert.Equal(1, pending.Price);
        Assert.Null(pending.StopPrice);
        Assert.Equal(1, pending.CreatedAtTurn);
        Assert.Equal(1 + 3, pending.ExpiresAtTurn);
    }

    [Fact]
    public async Task POST_orders_新規ゲームではpendingOrdersは空()
    {
        var created = await CreateGame();
        Assert.Empty(created.Player.PendingOrders);
    }

    [Fact]
    public async Task POST_orders_pendingOrdersにはコンピューター注文は含まれない()
    {
        var created = await CreateGame();

        // プレイヤー注文を出す。コンピューター注文も同時に板に積まれるが、
        // PendingOrders はプレイヤー本人のものに限る
        await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1));

        var game = await _client.GetFromJsonAsync<GameResponse>($"/api/games/{created.GameId}");
        Assert.NotNull(game);
        Assert.Single(game.Player.PendingOrders);

        // 板上にはコンピューター注文も含まれているはず
        var orderBook = await _client.GetFromJsonAsync<OrderBookResponse>(
            $"/api/admin/games/{created.GameId}/orderbook");
        Assert.NotNull(orderBook);
        Assert.True(orderBook.Orders.Count > 1);
    }

    [Fact]
    public async Task POST_orders_期限切れ後はpendingOrdersから消える()
    {
        var created = await CreateGame();

        // ExpiresInTurns=1 → ターン1で発注、ExpiresAtTurn=2
        await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/orders",
            new OrderRequest(Side: OrderSide.Buy, InstrumentId: 1, Quantity: 1, Price: 1, ExpiresInTurns: 1));

        // Wait でターン進めると expire される
        var waitResponse = await _client.PostAsync($"/api/games/{created.GameId}/wait", null);
        Assert.Equal(HttpStatusCode.OK, waitResponse.StatusCode);
        var game = await waitResponse.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Empty(game.Player.PendingOrders);
    }

    private async Task<GameResponse> CreateGame()
    {
        var response = await _client.PostAsync("/api/games", null);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        return game!;
    }
}
