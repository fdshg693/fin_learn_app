# Task 1: Razor Pages インフラと smoke test

[← Back to plan](../htmx-frontend.md)

Razor Pages を `FinLearn.Api` に有効化し、`/play/ping` が `200 OK` で `<h1>pong</h1>` 相当を返すまでを確認する。

**Files:**
- Modify: `src/FinLearn.Api/Program.cs`
- Create: `src/FinLearn.Api/Pages/_ViewImports.cshtml`
- Create: `src/FinLearn.Api/Pages/_ViewStart.cshtml`
- Create: `src/FinLearn.Api/Pages/Shared/_Layout.cshtml`
- Create: `src/FinLearn.Api/Pages/Play/Ping.cshtml`
- Create: `src/FinLearn.Api/Pages/Play/Ping.cshtml.cs`
- Create: `src/FinLearn.Api/wwwroot/htmx.min.js`
- Test: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs`

---

- [ ] **Step 1: 失敗テストを追加**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` を新規作成:

```csharp
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
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: FAIL（`/play/ping` も `/htmx.min.js` も 404）

- [ ] **Step 3: `Program.cs` に Razor Pages と静的ファイルを追加**

`src/FinLearn.Api/Program.cs` の `var app = builder.Build();` 直前に追記:

```csharp
    builder.Services.AddRazorPages();
```

`app.UseCors();` の直後に追記:

```csharp
    app.UseStaticFiles();
    app.MapRazorPages();
```

最終的な該当箇所はこうなる:

```csharp
    builder.Services.AddRazorPages();

    var app = builder.Build();

    app.UseCors();
    app.UseStaticFiles();
    app.MapRazorPages();
    app.MapGameEndpoints();
    app.MapAdminEndpoints();
```

- [ ] **Step 4: `_ViewImports.cshtml` を作成**

`src/FinLearn.Api/Pages/_ViewImports.cshtml`:

```cshtml
@namespace FinLearn.Api.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 5: `_ViewStart.cshtml` を作成**

`src/FinLearn.Api/Pages/_ViewStart.cshtml`:

```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 6: `_Layout.cshtml` を作成**

`src/FinLearn.Api/Pages/Shared/_Layout.cshtml`:

```cshtml
<!DOCTYPE html>
<html lang="ja">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] - 株売買シミュレーター (HTMX)</title>
    <link rel="stylesheet" href="~/site.css" />
    <script src="~/htmx.min.js" defer></script>
</head>
<body>
    <main>
        @RenderBody()
    </main>
</body>
</html>
```

- [ ] **Step 7: smoke test 用 Razor ページを作成**

`src/FinLearn.Api/Pages/Play/Ping.cshtml`:

```cshtml
@page
@model FinLearn.Api.Pages.Play.PingModel
@{
    ViewData["Title"] = "Ping";
}
<h1>pong</h1>
```

`src/FinLearn.Api/Pages/Play/Ping.cshtml.cs`:

```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class PingModel : PageModel
{
    public void OnGet() { }
}
```

- [ ] **Step 8: htmx.min.js を配置**

PowerShell で htmx 2.0.x をダウンロード:

```powershell
New-Item -ItemType Directory -Force -Path src\FinLearn.Api\wwwroot | Out-Null
Invoke-WebRequest -Uri "https://unpkg.com/htmx.org@2.0.4/dist/htmx.min.js" -OutFile src\FinLearn.Api\wwwroot\htmx.min.js
```

ダウンロード後、ファイル先頭が `(function` などで始まる JS であることを確認:

```powershell
Get-Content src\FinLearn.Api\wwwroot\htmx.min.js -TotalCount 1
```

Expected: 先頭 1 行が JavaScript（HTML や 404 ページではない）

- [ ] **Step 9: 空の `site.css` を作成**

`src/FinLearn.Api/wwwroot/site.css`:

```css
body { font-family: system-ui, -apple-system, sans-serif; margin: 1rem; }
```

- [ ] **Step 10: テストが通ることを確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter HtmxPagesTests`
Expected: PASS（2 件とも）

- [ ] **Step 11: 既存の API テストが壊れていないことを確認**

Run: `dotnet test`
Expected: 全件 PASS

- [ ] **Step 12: コミット**

```powershell
git add src/FinLearn.Api/Program.cs `
        src/FinLearn.Api/Pages `
        src/FinLearn.Api/wwwroot `
        tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "feat(htmx): scaffold Razor Pages + htmx.min.js with /play/ping smoke test"
```
