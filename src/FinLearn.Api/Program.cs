using FinLearn.Api.Dtos;
using FinLearn.Api.Services;
using FinLearn.Core;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GameStore>();
builder.Services.AddSingleton<Random>(_ => new Random());
builder.Services.AddTransient<TurnProcessor>(sp =>
{
    var random = sp.GetRequiredService<Random>();
    return new TurnProcessor(new ComputerTrader(random), new RandomPriceFluctuator(random));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

app.MapPost("/api/games", (GameStore store) =>
{
    var (gameId, game) = store.CreateGame();
    var exchange = new SimpleExchange(game.Prices, GameStore.Fee);
    return Results.Created($"/api/games/{gameId}", ToResponse(gameId, game, exchange));
});

app.MapGet("/api/games/{id}", (string id, GameStore store) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();
    var exchange = new SimpleExchange(game.Prices, GameStore.Fee);
    return Results.Ok(ToResponse(id, game, exchange));
});

app.MapPost("/api/games/{id}/buy", (string id, OrderRequest request, GameStore store, TurnProcessor processor) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var (result, warning) = processor.Buy(game, GameStore.Fee, request.InstrumentId, request.Quantity, request.Price);
    if (warning is null) store.UpdateGame(id, result);
    var exchange = new SimpleExchange(result.Prices, GameStore.Fee);
    return Results.Ok(ToResponse(id, result, exchange, warning));
});

app.MapPost("/api/games/{id}/sell", (string id, OrderRequest request, GameStore store, TurnProcessor processor) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var (result, warning) = processor.Sell(game, GameStore.Fee, request.InstrumentId, request.Quantity, request.Price);
    if (warning is null) store.UpdateGame(id, result);
    var exchange = new SimpleExchange(result.Prices, GameStore.Fee);
    return Results.Ok(ToResponse(id, result, exchange, warning));
});

app.MapPost("/api/games/{id}/wait", (string id, GameStore store, TurnProcessor processor) =>
{
    var game = store.GetGame(id);
    if (game is null) return Results.NotFound();

    var (result, _) = processor.Wait(game, GameStore.Fee);
    store.UpdateGame(id, result);
    var exchange = new SimpleExchange(result.Prices, GameStore.Fee);
    return Results.Ok(ToResponse(id, result, exchange));
});

app.Run();

static GameResponse ToResponse(string gameId, Game game, IExchange exchange, string? warning = null)
{
    var positions = game.Player.Portfolio.QuantityOf(0) >= 0
        ? game.Instruments
            .Select(i =>
            {
                var qty = game.Player.Portfolio.QuantityOf(i.Id);
                if (qty <= 0) return null;
                exchange.TryGetPrice(i.Id, out var price);
                return new PositionDto(i.Id, qty, price, qty * price);
            })
            .Where(p => p is not null)
            .Cast<PositionDto>()
            .ToList()
        : new List<PositionDto>();

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
