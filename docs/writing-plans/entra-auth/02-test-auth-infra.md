# Task 2: テスト用認証基盤

[← Back to plan](../entra-auth.md)

統合テストが Entra へ実際にトークン検証しに行かなくて済むよう、テスト用の認証スキームを用意する。`TestAuthHandler` はリクエストヘッダ `X-Test-Auth` で挙動を切り替える:

- ヘッダなし（既定）→ 認証成功 + スコープ `access_as_user` 付き（通常の API テスト用）
- `X-Test-Auth: none` → `NoResult`（＝未認証。401 検証用）
- `X-Test-Auth: noscope` → 認証成功だが `scp` クレームなし（403 検証用）

この1ハンドラ + 1ファクトリで設計書 §7 の3シナリオを賄う。本タスクでは認証ミドルウェアをまだ配線しない（Task 3 で配線）。よって挙動は変わらず、フィクスチャ差し替え後も全テストグリーンのまま。

> 設計書 §7 は `HtmxPagesTests` を「無改修」としているが、これは設計書側の見落とし。`HtmxPagesTests` は `CreateGameViaApi()`（および直接 `POST /api/games`）で**保護対象の `/api/games` を叩いてゲームを作る**ため、Task 3 で保護が掛かると 401 になりゲーム作成に失敗する（ゲーム作成に依存する複数メソッドが赤化）。よって `GameApiTests` と同じ1行フィクスチャ差し替えを `HtmxPagesTests` にも適用する（既定クライアントは `X-Test-Auth` ヘッダ無し＝認証成功+scp なので `/api/games` は通り、`/play` は元々未保護で無影響）。

**Files:**
- Create: `tests/FinLearn.Api.Tests/TestAuthHandler.cs`
- Create: `tests/FinLearn.Api.Tests/AuthTestWebApplicationFactory.cs`
- Modify: `tests/FinLearn.Api.Tests/GameApiTests.cs:9-16`
- Modify: `tests/FinLearn.Api.Tests/HtmxPagesTests.cs:7-16`

---

- [x] **Step 1: `TestAuthHandler` を作成**

`tests/FinLearn.Api.Tests/TestAuthHandler.cs`:

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging; // ILoggerFactory（テストプロジェクトは ImplicitUsings 非対応のため明示）
using Microsoft.Extensions.Options;

namespace FinLearn.Api.Tests;

/// <summary>
/// テスト専用の認証ハンドラ。リクエストヘッダ X-Test-Auth で挙動を切り替える:
/// (なし)=認証成功+scp、"none"=未認証(NoResult)、"noscope"=認証成功だがscpなし。
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var mode = Request.Headers["X-Test-Auth"].ToString();

        if (mode == "none")
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "test-user"),
            new("name", "テストユーザー"),
        };
        if (mode != "noscope")
            claims.Add(new Claim("scp", "access_as_user"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

- [x] **Step 2: カスタムファクトリを作成**

`tests/FinLearn.Api.Tests/AuthTestWebApplicationFactory.cs`:

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace FinLearn.Api.Tests;

/// <summary>
/// テスト用認証スキーム(TestAuthHandler)を既定スキームとして登録する WebApplicationFactory。
/// JwtBearer/Microsoft.Identity.Web のメタデータ取得は走らないため Entra への通信は発生しない。
/// </summary>
public class AuthTestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                    TestAuthHandler.SchemeName, _ => { });

            // 本番コードが AddMicrosoftIdentityWebApi で設定した既定スキームを
            // テスト用スキームへ強制的に上書きする。
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
```

- [x] **Step 3: ビルドが通ることを確認**

Run: `dotnet build tests/FinLearn.Api.Tests`
Expected: ビルド成功（`Microsoft.AspNetCore.Mvc.Testing` / `Microsoft.AspNetCore.TestHost` は既存参照、`AuthenticationHandler` は ASP.NET Core 共有フレームワークに含まれる）。

- [x] **Step 4: `GameApiTests` のフィクスチャをカスタムファクトリへ差し替え**

`tests/FinLearn.Api.Tests/GameApiTests.cs` の冒頭クラス宣言とコンストラクタ（現状 9-16 行）だけを変更する。**テストメソッド本体は一切変更しない**。

変更前:

```csharp
public class GameApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GameApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
```

変更後:

```csharp
public class GameApiTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GameApiTests(AuthTestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }
```

`GameApiTests.cs` 冒頭の `using Microsoft.AspNetCore.Mvc.Testing;` は `WebApplicationFactory<Program>` を直接参照しなくなるため未使用になる。情報レベルの「未使用 using」（CS8019/IDE0005、ビルド・テストは失敗しない）が出るだけなのでそのまま残してよい。気になる場合はこの1行を削除する（`AuthTestWebApplicationFactory` は同一名前空間 `FinLearn.Api.Tests` なので using 追加は不要）。

- [x] **Step 5: `HtmxPagesTests` のフィクスチャも同様に差し替え**

`tests/FinLearn.Api.Tests/HtmxPagesTests.cs` の冒頭クラス宣言・フィールド・コンストラクタ（現状 7-16 行）だけを変更する。**テストメソッド本体・`CreateGameViaApi`・`ExtractAntiforgeryToken` は一切変更しない**。

変更前:

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

変更後:

```csharp
public class HtmxPagesTests : IClassFixture<AuthTestWebApplicationFactory>
{
    private readonly AuthTestWebApplicationFactory factory;
    private readonly HttpClient _client;

    public HtmxPagesTests(AuthTestWebApplicationFactory factory)
    {
        this.factory = factory;
        _client = factory.CreateClient();
    }
```

> `factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false })`（既存 58 行）はカスタムファクトリでもそのまま動く。`using Microsoft.AspNetCore.Mvc.Testing;` は `WebApplicationFactoryClientOptions` で引き続き使われるため未使用にならない。

- [x] **Step 6: 既存テストが依然グリーンであることを確認**

Run: `dotnet test`
Expected: PASS（全件）。認証ミドルウェアは未配線で、`GameApiTests`・`HtmxPagesTests` の既定クライアントは `X-Test-Auth` ヘッダを送らないが、そもそも保護が無いので挙動不変。

- [x] **Step 7: コミット**

```powershell
git add tests/FinLearn.Api.Tests/TestAuthHandler.cs `
        tests/FinLearn.Api.Tests/AuthTestWebApplicationFactory.cs `
        tests/FinLearn.Api.Tests/GameApiTests.cs `
        tests/FinLearn.Api.Tests/HtmxPagesTests.cs
git commit -m "test(api): add test auth handler + factory, swap test fixtures"
```
