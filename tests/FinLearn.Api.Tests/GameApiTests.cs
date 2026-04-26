using System.Net;
using System.Net.Http.Json;
using FinLearn.Api.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinLearn.Api.Tests;

public class GameApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GameApiTests(WebApplicationFactory<Program> factory)
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

    [Fact]
    public async Task POST_buy_買い注文でターンが進む()
    {
        var created = await CreateGame();

        // 高い指値で確実に約定させる（コンピューター注文同士のマッチングで売り注文が減る可能性があるため）
        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/buy",
            new OrderRequest(InstrumentId: 1, Quantity: 1, Price: 150));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.Equal(2, game.Turn);
        Assert.Null(game.Warning);
    }

    [Fact]
    public async Task POST_buy_数量0でwarning付きターン不変()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/buy",
            new OrderRequest(InstrumentId: 1, Quantity: 0));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        Assert.NotNull(game.Warning);
        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public async Task POST_sell_保有なし売り注文でwarning付きだがターンは進む()
    {
        var created = await CreateGame();

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/sell",
            new OrderRequest(InstrumentId: 1, Quantity: 1));

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
            "/api/games/nonexistent/sell",
            new OrderRequest(InstrumentId: 1, Quantity: 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task POST_buy_存在しないゲームは404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/games/nonexistent/buy",
            new OrderRequest(InstrumentId: 1, Quantity: 1));
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
    public async Task POST_buy_sell_ラウンドトリップで売買できる()
    {
        var created = await CreateGame();

        // 買い
        var buyResponse = await _client.PostAsJsonAsync(
            $"/api/games/{created.GameId}/buy",
            new OrderRequest(InstrumentId: 1, Quantity: 1));
        var afterBuy = await buyResponse.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(afterBuy);
        Assert.Null(afterBuy.Warning);
        Assert.Equal(2, afterBuy.Turn);

        // 売り
        var sellResponse = await _client.PostAsJsonAsync(
            $"/api/games/{afterBuy.GameId}/sell",
            new OrderRequest(InstrumentId: 1, Quantity: 1));
        var afterSell = await sellResponse.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(afterSell);
        Assert.Null(afterSell.Warning);
        Assert.Equal(3, afterSell.Turn);
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
            $"/api/games/{created.GameId}/buy",
            new OrderRequest(InstrumentId: 1, Quantity: 1, Price: 1));

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

    private async Task<GameResponse> CreateGame()
    {
        var response = await _client.PostAsync("/api/games", null);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        return game!;
    }
}
