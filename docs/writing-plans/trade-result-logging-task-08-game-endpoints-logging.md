# Task 8: GameEndpoints で OrdersSubmitted / OrdersMatched をログ出力

親プラン: [trade-result-logging.md](./trade-result-logging.md)

**Files:**
- Modify: `src/FinLearn.Api/Endpoints/GameEndpoints.cs`

- [ ] **Step 1: GameEndpoints を以下に置き換え**

`src/FinLearn.Api/Endpoints/GameEndpoints.cs`:

```csharp
using FinLearn.Api.Dtos;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;

namespace FinLearn.Api.Endpoints;

public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games");

        group.MapPost("/", CreateGame);
        group.MapGet("/{id}", GetGame);
        group.MapPost("/{id}/buy", Buy);
        group.MapPost("/{id}/sell", Sell);
        group.MapPost("/{id}/wait", Wait);

        return group;
    }

    private static IResult CreateGame(GameStore store, IExchangeFactory exchangeFactory, GameConfig config)
    {
        var (gameId, game) = store.CreateGame();
        var exchange = exchangeFactory.Create(game.Prices, config.Fee);
        return Results.Created($"/api/games/{gameId}", GameMapper.ToResponse(gameId, game, exchange));
    }

    private static IResult GetGame(string id, GameStore store, IExchangeFactory exchangeFactory, GameConfig config)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();
        var exchange = exchangeFactory.Create(game.Prices, config.Fee);
        return Results.Ok(GameMapper.ToResponse(id, game, exchange));
    }

    private static IResult Buy(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<Program> logger)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
            (g, fee, req) => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
    }

    private static IResult Sell(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<Program> logger)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
            (g, fee, req) => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
    }

    private static IResult Wait(string id, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<Program> logger)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();

        var turn = processor.Wait(game, config.Fee);
        store.UpdateGame(id, turn.Game);
        LogTurnEvents(logger, id, turn);

        var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
        var recentTrades = store.GetRecentTrades(id);
        return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, recentTrades: recentTrades));
    }

    private static IResult ProcessOrder(
        string id, OrderRequest request, GameStore store, TurnProcessor processor,
        IExchangeFactory exchangeFactory, GameConfig config, ILogger<Program> logger,
        Func<Game, int, OrderRequest, TurnResult> action)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();

        var turn = action(game, config.Fee, request);
        if (turn.Warning is null)
        {
            store.UpdateGame(id, turn.Game);
            if (turn.Trade is not null) store.AddTrade(id, turn.Trade);
        }
        LogTurnEvents(logger, id, turn);

        var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
        var recentTrades = store.GetRecentTrades(id);
        return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, turn.Warning, recentTrades));
    }

    private static void LogTurnEvents(ILogger logger, string gameId, TurnResult result)
    {
        logger.LogInformation(
            "OrdersSubmitted Game={GameId} Turn={Turn} Count={Count} Warning={Warning} {@Orders}",
            gameId, result.ProcessedTurn, result.SubmittedOrders.Count,
            result.Warning, result.SubmittedOrders);

        logger.LogInformation(
            "OrdersMatched Game={GameId} Turn={Turn} Count={Count} {@Fills}",
            gameId, result.ProcessedTurn, result.Fills.Count, result.Fills);
    }
}
```

設計判断:

- バリデーション失敗時 (`SubmittedOrders` も `Fills` も空) でも 2 イベントは出す。Warning は OrdersSubmitted に含まれるので、jq で `Warning != null` 検索ができる仕様 §7.4 と一致する。
- 404 (`game is null`) 時はログを出さない（処理対象がそもそも存在しないため）。

- [ ] **Step 2: ビルド + 全テスト**

Run: `dotnet build && dotnet test`
Expected: PASS（既存テストすべて。`ILogger<Program>` パラメータが追加されるが Minimal API の DI が解決する）

- [ ] **Step 3: 手動スモークテスト**

サーバを起動して 1 ターン進める:

```bash
dotnet run --project src/FinLearn.Api/FinLearn.Api.csproj &
sleep 3
GAMEID=$(curl -s -X POST http://localhost:5000/api/games | jq -r '.gameId')
curl -s -X POST http://localhost:5000/api/games/$GAMEID/wait > /dev/null
```

`src/FinLearn.Api/logs/finlearn-{YYYYMMDD}.log` を覗いて以下を確認:

- `"@m":"OrdersSubmitted ..."` を含む行が 1 件以上ある
- `"@m":"OrdersMatched ..."` を含む行が 1 件以上ある
- `"Orders"` 配列にコンピューター注文オブジェクトがシリアライズされている（`Id`, `TraderId`, `Side` などのフィールドが見える）
- `"Turn":1` (Wait は元ゲームのターンを処理する)

確認後サーバ停止 (`kill %1` 等)。

- [ ] **Step 4: コミット**

```bash
git add src/FinLearn.Api/Endpoints/GameEndpoints.cs
git commit -m "feat(api): log OrdersSubmitted and OrdersMatched per turn"
```
