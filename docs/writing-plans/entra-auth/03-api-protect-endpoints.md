# Task 3: API エンドポイント保護

[← Back to plan](../entra-auth.md)

`Program.cs` に Entra JWT 認証と名前付きポリシー `ApiScope` を配線し、`/api/games`・`/api/admin` ルートグループにのみ適用する。グローバルフォールバックポリシーは設定しない（`/play`・`/`・静的ファイルを未保護に保つ）。`MapGameEndpoints()` / `MapAdminEndpoints()` の戻り値（`RouteGroupBuilder`）を受けて `.RequireAuthorization("ApiScope")` を付ける（現状は戻り値を破棄しているので呼び出し側の書き換えが必須）。

**Files:**
- Modify: `src/FinLearn.Api/Program.cs:1-8`（using 追加）
- Modify: `src/FinLearn.Api/Program.cs:76-84`（認可登録・ミドルウェア・ルートグループ保護）
- Test: `tests/FinLearn.Api.Tests/AuthApiTests.cs`

---

- [ ] **Step 1: 失敗テストを追加**

`tests/FinLearn.Api.Tests/AuthApiTests.cs` を新規作成:

```csharp
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
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter AuthApiTests`
Expected: FAIL。認証未配線のため `401` / `403` を期待する3テストが赤（実際は 201/200 が返る）。`/play`・`/`・スコープありの3テストは緑。

- [ ] **Step 3: `Program.cs` に using を追加**

`src/FinLearn.Api/Program.cs` の先頭 using 群（1-8 行）の末尾に2行追加する:

```csharp
using System.Text.Json.Serialization;
using FinLearn.Api.Endpoints;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Serilog;
using Serilog.Filters;
using Serilog.Formatting.Compact;
```

- [ ] **Step 4: 認証・認可サービスを登録**

`src/FinLearn.Api/Program.cs` の `builder.Services.AddRazorPages();`（76 行）の直前に追加する:

```csharp
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("ApiScope", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireScope("access_as_user");
        });
    });

    builder.Services.AddRazorPages();
```

> `RequireScope` は `Microsoft.Identity.Web` 名前空間の `AuthorizationPolicyBuilder` 拡張（Step 3 の using で解決）。`[RequiredScope]` 属性やエンドポイント単位の `RequireScope` ラムダは使わない（機構を1つに統一）。

- [ ] **Step 5: ミドルウェアとルートグループ保護を配線**

`src/FinLearn.Api/Program.cs` の現状:

```csharp
    app.UseCors();
    app.UseStaticFiles();
    app.MapRazorPages();
    app.MapGameEndpoints();
    app.MapAdminEndpoints();
```

を以下に書き換える（`UseAuthentication`/`UseAuthorization` を `UseCors` の後・エンドポイントマップの前に挿入し、2つのルートグループの戻り値を受けて保護する）:

```csharp
    app.UseCors();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapRazorPages();
    app.MapGameEndpoints().RequireAuthorization("ApiScope");
    app.MapAdminEndpoints().RequireAuthorization("ApiScope");
```

> `MapRazorPages()` には `RequireAuthorization` を付けない＝`/play` は未保護のまま。`app.MapGet("/", ...)` も未保護。グローバルフォールバックポリシーは設定しない。`GameEndpoints.cs` / `AdminEndpoints.cs` 本体は変更しない（戻り値の `RouteGroupBuilder` を呼び出し側で受けるだけ）。

- [ ] **Step 6: テストが通ることを確認**

Run: `dotnet test tests/FinLearn.Api.Tests --filter AuthApiTests`
Expected: PASS（6件）。`none`→401、`noscope`→403、ヘッダなし→201、`/play`・`/`→200。

- [ ] **Step 7: 既存の API・HTMX テストが壊れていないことを確認**

Run: `dotnet test`
Expected: 全件 PASS。`GameApiTests` は既定クライアント（ヘッダなし＝認証成功+scp）で従来通り。`HtmxPagesTests` は `/play`・`/api/games`（テスト用認証で通る）ともグリーン。

- [ ] **Step 8: コミット**

```powershell
git add src/FinLearn.Api/Program.cs `
        tests/FinLearn.Api.Tests/AuthApiTests.cs
git commit -m "feat(api): protect /api/games and /api/admin with Entra ApiScope policy"
```
