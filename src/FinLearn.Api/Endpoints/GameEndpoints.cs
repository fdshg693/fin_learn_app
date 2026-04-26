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

    private static IResult Buy(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config,
            (g, fee, req) => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
    }

    private static IResult Sell(string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
    {
        return ProcessOrder(id, request, store, processor, exchangeFactory, config,
            (g, fee, req) => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
    }

    private static IResult Wait(string id, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config)
    {
        var game = store.GetGame(id);
        if (game is null) return Results.NotFound();

        var turn = processor.Wait(game, config.Fee);
        store.UpdateGame(id, turn.Game);
        var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
        var recentTrades = store.GetRecentTrades(id);
        return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, recentTrades: recentTrades));
    }

    private static IResult ProcessOrder(
        string id, OrderRequest request, GameStore store, TurnProcessor processor,
        IExchangeFactory exchangeFactory, GameConfig config,
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
        var exchange = exchangeFactory.Create(turn.Game.Prices, config.Fee);
        var recentTrades = store.GetRecentTrades(id);
        return Results.Ok(GameMapper.ToResponse(id, turn.Game, exchange, turn.Warning, recentTrades));
    }
}
