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

    private static IResult Buy(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
            (g, fee, req) => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
    }

    private static IResult Sell(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config, ILogger<OrderLog> logger)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config, logger,
            (g, fee, req) => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
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
