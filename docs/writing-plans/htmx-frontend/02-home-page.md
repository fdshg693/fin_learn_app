# Task 2: ホーム画面 — ゲーム作成

[← Back to plan](../htmx-frontend.md)

`/play` で「ゲーム開始」ボタンを表示。クリックで POST → `GameStore.CreateGame` を呼び `/play/{id}` へリダイレクトする。リダイレクト先のページ実装は Task 3 で行うため、ここでは Task 3 の Razor ページが未実装でも `Location` ヘッダだけ検証する。

**Files:**
- Create: `src/FinLearn.Api/Pages/Play/Index.cshtml`
- Create: `src/FinLearn.Api/Pages/Play/Index.cshtml.cs`
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`
- Delete: `src/FinLearn.Api/Pages/Play/Ping.cshtml`（Index で代替するため不要）
- Delete: `src/FinLearn.Api/Pages/Play/Ping.cshtml.cs`

---

- [ ] **Step 1: 失敗テストを追加**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` に追加（既存の `GET_play_ping_は200で本文にpongを含む` は次の Step で削除する）:

```csharp
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
        // Re-issue with the no-redirect client
        var response = await clientNoRedirect.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/play/", response.Headers.Location!.ToString());
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var marker = "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = html.IndexOf('"', start);
        return html.Substring(start, end - start);
    }
```

クラスシグネチャを変更してフィクスチャを保持できるようにする:

```csharp
public class HtmxPagesTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient _client;

    public HtmxPagesTests(WebApplicationFactory<Program> factory)
    {
        this.factory = factory;
        _client = factory.CreateClient();
    }
```

既存の `GET_play_ping_は200で本文にpongを含む` テストは削除（Ping ページを廃止するため）。`GET_htmx_min_jsを200で配信できる` は残す。

- [ ] **Step 2: 失敗確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 新規 2 件 FAIL（`/play` が 404）

- [ ] **Step 3: Ping ページを削除**

```powershell
Remove-Item src\FinLearn.Api\Pages\Play\Ping.cshtml
Remove-Item src\FinLearn.Api\Pages\Play\Ping.cshtml.cs
```

- [ ] **Step 4: ホーム Razor ページの View を作成**

`src/FinLearn.Api/Pages/Play/Index.cshtml`:

```cshtml
@page "/play"
@model FinLearn.Api.Pages.Play.IndexModel
@{
    ViewData["Title"] = "ホーム";
}
<section style="text-align:center; padding:4rem 1rem;">
    <h1>株売買シミュレーター (HTMX)</h1>
    <p>株取引を体験してみよう</p>
    <form method="post">
        <button type="submit">ゲーム開始</button>
    </form>
</section>
```

注: Razor Pages のフォームは `<form method="post">` だけで自動的に antiforgery トークンが埋め込まれる。

- [ ] **Step 5: ホーム Razor ページの PageModel を作成**

`src/FinLearn.Api/Pages/Play/Index.cshtml.cs`:

```csharp
using FinLearn.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class IndexModel : PageModel
{
    private readonly GameStore _store;

    public IndexModel(GameStore store)
    {
        _store = store;
    }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        var (gameId, _) = _store.CreateGame();
        return Redirect($"/play/{gameId}");
    }
}
```

- [ ] **Step 6: テスト確認（リダイレクト先 404 は許容）**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: 3 件 PASS（GET, POST, htmx.min.js）

リダイレクト先 `/play/{id}` の Razor ページは Task 3 で実装するため、現状 GET すると 404 になるが、本タスクはリダイレクト発生のみを検証する。

- [ ] **Step 7: 全テスト確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 8: コミット**

```powershell
git add src/FinLearn.Api/Pages/Play tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): /play home page with start-game form"
```
