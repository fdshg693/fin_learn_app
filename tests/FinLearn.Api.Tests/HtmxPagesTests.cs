using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinLearn.Api.Tests;

public class HtmxPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient _client;

    public HtmxPagesTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_root_は_play_へのリンクを含むHTMLを返す()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("/play", body);
    }

    [Fact]
    public async Task GET_play_でゲーム開始ボタンを含むHTMLを返す()
    {
        var response = await _client.GetAsync("/play");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ゲーム開始", body);
        Assert.Contains("<form", body);
        Assert.Contains("method=\"post\"", body);
    }

    [Fact]
    public async Task POST_play_は新規ゲームを作成して_play_idへリダイレクトする()
    {
        // Razor Pages の antiforgery を回避するためのリクエストオプションは
        // テスト環境ではデフォルトで通る（DataProtection が同一プロセス）
        var initial = await _client.GetAsync("/play");
        var token = ExtractAntiforgeryToken(await initial.Content.ReadAsStringAsync());
        var cookie = initial.Headers.GetValues("Set-Cookie").First(c => c.Contains("Antiforgery"));

        var request = new HttpRequestMessage(HttpMethod.Post, "/play")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token,
            }),
        };
        request.Headers.Add("Cookie", cookie.Split(';')[0]);

        var clientNoRedirect = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        var response = await clientNoRedirect.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/play/", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task GET_play_id_でゲーム画面の全パネル見出しを含むHTMLを返す()
    {
        var create = await _client.PostAsync("/api/games", null);
        var created = await create.Content.ReadFromJsonAsync<FinLearn.Api.Dtos.GameResponse>();

        var response = await _client.GetAsync($"/play/{created!.GameId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"game\"", body);
        Assert.Contains("ターン", body);
        Assert.Contains("現金", body);
        Assert.Contains("未約定注文", body);
        Assert.Contains("銘柄一覧", body);
        Assert.Contains("保有ポジション", body);
        Assert.Contains("注文入力", body);
        // 「注文板」見出しは hx-trigger="load" の遅延注入で初回 HTML には含まれないため、
        // プレースホルダ div の存在のみを確認する
        Assert.Contains("id=\"orderbook\"", body);
    }

    [Fact]
    public async Task GET_play_id_存在しないゲームは404()
    {
        var response = await _client.GetAsync("/play/nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GET_htmx_min_jsを200で配信できる()
    {
        var response = await _client.GetAsync("/htmx.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

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
    public async Task GET_play_id_orderbook_は注文板パネルだけのHTMLを返す()
    {
        var gameId = await CreateGameViaApi();

        var response = await _client.GetAsync($"/play/{gameId}?handler=OrderBook&page=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("id=\"orderbook\"", body);
        Assert.DoesNotContain("id=\"game\"", body);
        Assert.DoesNotContain("注文入力", body);
    }

    [Fact]
    public async Task GET_play_id_orderbook_pageは1未満で400()
    {
        var gameId = await CreateGameViaApi();

        var response = await _client.GetAsync($"/play/{gameId}?handler=OrderBook&page=0");

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

    private static string ExtractAntiforgeryToken(string html)
    {
        var marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        return html.Substring(start, end - start);
    }
}
