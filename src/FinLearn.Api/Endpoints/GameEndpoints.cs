using FinLearn.Api.Dtos;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;

namespace FinLearn.Api.Endpoints;

/// <summary>
/// 注文関連ログの SourceContext マーカー。Serilog のフィルタ用。
/// </summary>
public sealed class OrderLog { }

public static class GameEndpoints
{
    public static RouteGroupBuilder MapGameEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/games");

        group.MapPost("/", CreateGame);
        group.MapGet("/{id}", GetGame);
        group.MapPost("/{id}/orders", PlaceOrder);
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

    private static IResult PlaceOrder(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger)
    {
        if (request.Side is null)
        {
            return Results.BadRequest(new { error = "side は必須です（\"Buy\" または \"Sell\"）" });
        }
        if (request.Quantity <= 0)
        {
            return Results.BadRequest(new { error = "quantity は 1 以上を指定してください" });
        }
        if (request.Price is not null && request.Price <= 0)
        {
            return Results.BadRequest(new { error = "price は 1 以上を指定してください" });
        }
        if (request.StopPrice is not null && request.StopPrice <= 0)
        {
            return Results.BadRequest(new { error = "stopPrice は 1 以上を指定してください" });
        }

        return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
            (g, fee, req) => req.Side switch
            {
                OrderSide.Buy => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice),
                OrderSide.Sell => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice),
                _ => throw new ArgumentOutOfRangeException(nameof(request), req.Side, "Unknown order side")
            });
    }

    private static IResult Wait(string id, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger)
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
        IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger,
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
