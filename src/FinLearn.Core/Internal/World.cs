namespace FinLearn.Core;

/// <summary>
/// ターン処理中の世界スナップショット。
/// Pipeline / Handler は World → World の関数として動く。
/// </summary>
internal sealed record World(
    OrderBook Book,
    IReadOnlyDictionary<string, Portfolio> Portfolios,
    int NextOrderId,
    IExchange Exchange,
    int Fee,
    string PlayerName,
    int Turn,
    IReadOnlyList<Instrument> Instruments,
    IReadOnlyDictionary<int, int> Prices)
{
    /// <summary>Player の Portfolio を観察する (dict 直接アクセスを排除)。</summary>
    public Portfolio PlayerPortfolio => Portfolios[PlayerName];

    public World WithBook(OrderBook book) => this with { Book = book };

    public World WithPortfolios(IReadOnlyDictionary<string, Portfolio> portfolios) =>
        this with { Portfolios = portfolios };

    /// <summary>Player の Portfolio だけを差し替える (dict は新インスタンス)。</summary>
    public World WithPlayerPortfolio(Portfolio playerPortfolio)
    {
        var dict = new Dictionary<string, Portfolio>(Portfolios) { [PlayerName] = playerPortfolio };
        return this with { Portfolios = dict };
    }

    public World WithNextOrderId(int nextOrderId) => this with { NextOrderId = nextOrderId };

    /// <summary>
    /// Game から World を構築する。Player.Portfolio + ComputerPortfolios を統合 dict にする。
    /// </summary>
    public static World FromGame(Game game, int fee, IExchange exchange)
    {
        var portfolios = new Dictionary<string, Portfolio>(game.ComputerPortfolios.Count + 1);
        foreach (var (id, pf) in game.ComputerPortfolios)
            portfolios[id] = pf;
        portfolios[game.Player.Name] = game.Player.Portfolio;

        return new World(
            Book: game.OrderBook,
            Portfolios: portfolios,
            NextOrderId: game.NextOrderId,
            Exchange: exchange,
            Fee: fee,
            PlayerName: game.Player.Name,
            Turn: game.Turn,
            Instruments: game.Instruments,
            Prices: game.Prices);
    }
}
