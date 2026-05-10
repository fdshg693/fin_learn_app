using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinLearn.Api.Tests;

public class HtmxPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HtmxPagesTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_play_ping_は200で本文にpongを含む()
    {
        var response = await _client.GetAsync("/play/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("text/html", response.Content.Headers.ContentType?.ToString() ?? "");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pong", body);
    }

    [Fact]
    public async Task GET_htmx_min_jsを200で配信できる()
    {
        var response = await _client.GetAsync("/htmx.min.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
