using System.Collections.Concurrent;
using FinLearn.Core;

namespace FinLearn.Api.Services;

public sealed class GameStore
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly GameConfig _config;

    public GameStore(GameConfig config)
    {
        _config = config;
    }

    public (string GameId, Game Game) CreateGame()
    {
        var gameId = Guid.NewGuid().ToString("N");
        var instruments = Enumerable.Range(1, _config.InstrumentCount)
            .Select(id => new Instrument(id))
            .ToList();
        var prices = instruments.ToDictionary(i => i.Id, _ => _config.InitialPrice)
            .AsReadOnly();
        var game = new Game(instruments, prices);
        _games[gameId] = game;
        return (gameId, game);
    }

    public Game? GetGame(string gameId)
    {
        return _games.TryGetValue(gameId, out var game) ? game : null;
    }

    public void UpdateGame(string gameId, Game game)
    {
        _games[gameId] = game;
    }
}
