using FinLearn.Api.Dtos;
using FinLearn.Api.Services;
using FinLearn.Core;

var builder = WebApplication.CreateBuilder(args);

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

app.MapPost("/api/games", (GameStore store, IExchangeFactory exchangeFactory, GameConfig config) =>
{
    var (gameId, game) = store.CreateGame();
    var exchange = exchangeFactory.Create(game.Prices, config.Fee);
    return Results.Created($"/api/games/{gameId}", ToResponse(gameId, game, exchange));
});

app.MapGet("/api/games/{id}", (string id, GameStore store, IExchangeFactory exchangeFactory, GameConfig config) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();
    var exchange = exchangeFactory.Create(game.Prices, config.Fee);
    return Results.Ok(ToResponse(id, game, exchange));
});

app.MapPost("/api/games/{id}/buy", (string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config) =>
{
    return ProcessOrder(id, request, store, processor, exchangeFactory, config,
        (g, fee, req) => processor.Buy(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
});

app.MapPost("/api/games/{id}/sell", (string id, OrderRequest request, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config) =>
{
    return ProcessOrder(id, request, store, processor, exchangeFactory, config,
        (g, fee, req) => processor.Sell(g, fee, req.InstrumentId, req.Quantity, req.Price, req.StopPrice));
});

app.MapPost("/api/games/{id}/wait", (string id, GameStore store, TurnProcessor processor, IExchangeFactory exchangeFactory, GameConfig config) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var (result, _) = processor.Wait(game, config.Fee);
    store.UpdateGame(id, result);
    var exchange = exchangeFactory.Create(result.Prices, config.Fee);
    return Results.Ok(ToResponse(id, result, exchange));
});

app.Run();

static IResult ProcessOrder(
    string id, OrderRequest request, GameStore store, TurnProcessor processor,
    IExchangeFactory exchangeFactory, GameConfig config,
    Func<Game, int, OrderRequest, (Game Result, string? Warning)> action)
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var (result, warning) = action(game, config.Fee, request);
    if (warning is null) store.UpdateGame(id, result);
    var exchange = exchangeFactory.Create(result.Prices, config.Fee);
    return Results.Ok(ToResponse(id, result, exchange, warning));
}

static GameResponse ToResponse(string gameId, Game game, IExchange exchange, string? warning = null)
{
    var positions = game.Instruments
        .Select(i =>
        {
            var qty = game.Player.Portfolio.QuantityOf(i.Id);
            if (qty <= 0) return null;
            exchange.TryGetPrice(i.Id, out var price);
            return new PositionDto(i.Id, qty, price, qty * price);
        })
        .Where(p => p is not null)
        .Cast<PositionDto>()
        .ToList();

    var playerDto = new PlayerDto(
        Name: game.Player.Name,
        Cash: game.Player.Portfolio.Cash,
        Positions: positions,
        TotalAssets: game.Player.Portfolio.TotalAmount(exchange),
        ProfitLoss: game.Player.ProfitLoss(exchange));

    var instruments = game.Instruments
        .Select(i =>
        {
            exchange.TryGetPrice(i.Id, out var price);
            return new InstrumentDto(i.Id, price);
        })
        .ToList();

    return new GameResponse(gameId, game.Turn, playerDto, instruments, warning);
}

public partial class Program { }
