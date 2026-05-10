using FinLearn.Api.Dtos;
using FinLearn.Api.Endpoints;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

[IgnoreAntiforgeryToken]
public class GameModel : PageModel
{
    private readonly GameStore _store;
    private readonly TurnProcessor _processor;
    private readonly IExchangeFactory _exchangeFactory;
    private readonly GameConfig _config;
    private readonly ILogger<OrderLog> _logger;

    public GameModel(
        GameStore store,
        TurnProcessor processor,
        IExchangeFactory exchangeFactory,
        GameConfig config,
        ILogger<OrderLog> logger)
    {
        _store = store;
        _processor = processor;
        _exchangeFactory = exchangeFactory;
        _config = config;
        _logger = logger;
    }

    public GameResponse Game { get; private set; } = default!;

    [BindProperty(SupportsGet = false)] public int InstrumentId { get; set; }
    [BindProperty(SupportsGet = false)] public int Quantity { get; set; }
    [BindProperty(SupportsGet = false)] public int? Price { get; set; }

    public IActionResult OnGet(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();
        Game = BuildResponse(id, game, warning: null);
        return Page();
    }

    public IActionResult OnPostBuy(string id) => HandleTrade(id, OrderSide.Buy);
    public IActionResult OnPostSell(string id) => HandleTrade(id, OrderSide.Sell);

    public IActionResult OnPostWait(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();

        var turn = _processor.Wait(game, _config.Fee);
        _store.UpdateGame(id, turn.Game);
        LogTurn(id, turn);

        Game = BuildResponse(id, turn.Game, turn.Warning);
        return Partial("_GameContainer", Game);
    }

    private IActionResult HandleTrade(string id, OrderSide side)
    {
        if (Quantity <= 0)
            return BadRequest(new { error = "quantity は 1 以上を指定してください" });
        if (Price is not null && Price <= 0)
            return BadRequest(new { error = "price は 1 以上を指定してください" });

        var game = _store.GetGame(id);
        if (game is null) return NotFound();

        var expiresInTurns = GameRules.DefaultOrderTtl;
        var turn = side == OrderSide.Buy
            ? _processor.Buy(game, _config.Fee, InstrumentId, Quantity, Price, null, expiresInTurns)
            : _processor.Sell(game, _config.Fee, InstrumentId, Quantity, Price, null, expiresInTurns);

        if (turn.Warning is null)
        {
            _store.UpdateGame(id, turn.Game);
            if (turn.Trade is not null && turn.Trade.FilledQuantity > 0)
                _store.AddTrade(id, turn.Trade);
        }
        LogTurn(id, turn);

        Game = BuildResponse(id, turn.Game, turn.Warning);
        return Partial("_GameContainer", Game);
    }

    private GameResponse BuildResponse(string id, Game game, string? warning)
    {
        var exchange = _exchangeFactory.Create(game.Prices, _config.Fee);
        var recentTrades = _store.GetRecentTrades(id);
        return GameMapper.ToResponse(id, game, exchange, warning, recentTrades);
    }

    private void LogTurn(string id, TurnResult result)
    {
        _logger.LogInformation(
            "OrdersSubmitted Game={GameId} Turn={Turn} Count={Count} Warning={Warning} {@Orders}",
            id, result.ProcessedTurn, result.SubmittedOrders.Count,
            result.Warning, result.SubmittedOrders);
        _logger.LogInformation(
            "OrdersMatched Game={GameId} Turn={Turn} Count={Count} {@Fills}",
            id, result.ProcessedTurn, result.Fills.Count, result.Fills);
    }
}
