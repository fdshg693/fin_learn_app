using FinLearn.Api.Dtos;
using FinLearn.Api.Mappers;
using FinLearn.Api.Services;
using FinLearn.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FinLearn.Api.Pages.Play;

public class GameModel : PageModel
{
    private readonly GameStore _store;
    private readonly IExchangeFactory _exchangeFactory;
    private readonly GameConfig _config;

    public GameModel(GameStore store, IExchangeFactory exchangeFactory, GameConfig config)
    {
        _store = store;
        _exchangeFactory = exchangeFactory;
        _config = config;
    }

    public GameResponse Game { get; private set; } = default!;

    public IActionResult OnGet(string id)
    {
        var game = _store.GetGame(id);
        if (game is null) return NotFound();
        var exchange = _exchangeFactory.Create(game.Prices, _config.Fee);
        var recentTrades = _store.GetRecentTrades(id);
        Game = GameMapper.ToResponse(id, game, exchange, recentTrades: recentTrades);
        return Page();
    }
}
