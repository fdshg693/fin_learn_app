using System.Net;
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
    public async Task GET_htmx_min_jsを200で配信できる()
    {
        var response = await _client.GetAsync("/htmx.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        return html.Substring(start, end - start);
    }
}
