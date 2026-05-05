using System.Collections.Concurrent;
using FinLearn.Core;

namespace FinLearn.Api.Services;

public sealed class GameStore
{
    private readonly ConcurrentDictionary<string, Game> _games = new();
    private readonly ConcurrentDictionary<string, List<TradeResult>> _tradeHistories = new();
    private readonly ConcurrentDictionary<string, object> _locks = new();
    private readonly GameConfig _config;
    private readonly GameStoreConfig _storeConfig;

    public GameStore(GameConfig config, GameStoreConfig storeConfig)
    {
        _config = config;
        _storeConfig = storeConfig;
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

    public void AddTrade(string gameId, TradeResult trade)
    {
        var history = _tradeHistories.GetOrAdd(gameId, _ => new List<TradeResult>());
        lock (history)
        {
            history.Add(trade);
            if (history.Count > _storeConfig.MaxRecentTrades)
                history.RemoveAt(0);
        }
    }

    public IReadOnlyList<TradeResult> GetRecentTrades(string gameId)
    {
        if (!_tradeHistories.TryGetValue(gameId, out var history))
            return Array.Empty<TradeResult>();
        lock (history)
        {
            return history.ToList().AsReadOnly();
        }
    }

    /// <summary>
    /// ゲームの取得→更新をアトミックに実行する。
    /// 同一ゲームIDへの並行アクセスをロックで直列化する。
    /// </summary>
    public Game? ExecuteUpdate(string gameId, Func<Game, Game?> updater)
    {
        var lockObj = _locks.GetOrAdd(gameId, _ => new object());
        lock (lockObj)
        {
            var game = GetGame(gameId);
            if (game is null) return null;
            var updated = updater(game);
            if (updated is not null)
                UpdateGame(gameId, updated);
            return updated;
        }
    }
}
