# Task 7: Program.cs で Serilog を構成

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Api/Program.cs`

- [ ] **Step 1: Program.cs を Serilog 構成入りに置き換え**

`src/FinLearn.Api/Program.cs` を以下に置き換え:

```csharp
using FinLearn.Api.Endpoints;
using FinLearn.Api.Services;
using FinLearn.Core;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}")
    .WriteTo.File(
        formatter: new CompactJsonFormatter(),
        path: "logs/finlearn-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddSingleton<GameConfig>();
    builder.Services.AddSingleton<GameStore>();
    builder.Services.AddSingleton<IExchangeFactory, SimpleExchangeFactory>();
    builder.Services.AddTransient<TurnProcessor>(sp =>
    {
        var exchangeFactory = sp.GetRequiredService<IExchangeFactory>();
        return new TurnProcessor(new ComputerTrader(Random.Shared), new Market(), new RandomPriceFluctuator(Random.Shared), exchangeFactory);
    });

    var corsOrigins = builder.Configuration["CORS_ALLOWED_ORIGINS"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        ?? ["http://localhost:5173"];

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    app.UseCors();
    app.MapGameEndpoints();
    app.MapAdminEndpoints();

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
```

`try/finally` は Serilog ベストプラクティス（プロセス終了時に未フラッシュのバッファを書き出す）。

- [ ] **Step 2: ビルド確認**

Run: `dotnet build src/FinLearn.Api/FinLearn.Api.csproj`
Expected: ビルド成功

- [ ] **Step 3: 起動スモークテスト**

Run: `dotnet run --project src/FinLearn.Api/FinLearn.Api.csproj`（5 秒程度で Ctrl+C）
Expected: コンソールに Serilog 形式のログ（`HH:mm:ss [INF] ...`）が出力される。

ログファイルの場所: `dotnet run --project ...csproj` は `src/FinLearn.Api/` をプロセスのカレントディレクトリにする。Serilog の sink は相対パス `logs/finlearn-.log` を使うので、生成物は `src/FinLearn.Api/logs/finlearn-{YYYYMMDD}.log` に出る。別シェル/別 CWD から `dotnet src/FinLearn.Api/bin/.../FinLearn.Api.dll` 等で起動した場合はそのシェルの CWD 配下に出力されるため注意。

- [ ] **Step 4: API 統合テストが回帰していないか確認**

Run: `dotnet test tests/FinLearn.Api.Tests/FinLearn.Api.Tests.csproj`
Expected: PASS（既存 11 テスト全て通る）

- [ ] **Step 5: コミット**

```bash
git add src/FinLearn.Api/Program.cs
git commit -m "feat(api): configure Serilog console + daily JSON file sink"
```
