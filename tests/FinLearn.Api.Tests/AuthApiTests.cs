using System.Net;

namespace FinLearn.Api.Tests;

public class AuthApiTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly AuthTestWebApplicationFactory _factory;

    public AuthApiTests(AuthTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? testAuth)
    {
        var client = _factory.CreateClient();
        var req = new HttpRequestMessage(method, path);
        if (testAuth is not null) req.Headers.Add("X-Test-Auth", testAuth);
        return await client.SendAsync(req);
    }

    [Fact]
    public async Task 認証なしで_POST_api_games_は401()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/games", "none");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task 認証なしで_GET_api_admin_orderbook_は401()
    {
        var res = await SendAsync(HttpMethod.Get, "/api/admin/games/x/orderbook", "none");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task スコープなしトークンで_POST_api_games_は403()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/games", "noscope");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task スコープありトークンで_POST_api_games_は201()
    {
        var res = await SendAsync(HttpMethod.Post, "/api/games", null);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }

    [Fact]
    public async Task play_は認証なしでも200_未保護の回帰確認()
    {
        var res = await SendAsync(HttpMethod.Get, "/play", "none");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task ルート_は認証なしでも200_未保護の回帰確認()
    {
        var res = await SendAsync(HttpMethod.Get, "/", "none");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }
}
