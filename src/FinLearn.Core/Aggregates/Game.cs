namespace FinLearn.Core;

/// <summary>
/// ゲームの状態スナップショット。ワークフロー制御は TurnProcessor が担う。
/// </summary>
public sealed class Game
{
    public Game(IReadOnlyList<Instrument> instruments, IReadOnlyDictionary<int, int> prices)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1, instruments, prices,
            CreateInitialComputerPortfolios())
    {
    }

    public Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IReadOnlyList<Instrument> instruments, IReadOnlyDictionary<int, int> prices)
        : this(player, turn, orderBook, nextOrderId, instruments, prices,
            CreateInitialComputerPortfolios())
    {
    }

    public Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IReadOnlyList<Instrument> instruments, IReadOnlyDictionary<int, int> prices,
        IReadOnlyDictionary<string, Portfolio> computerPortfolios)
    {
        Player = player;
        Turn = turn;
        OrderBook = orderBook;
        NextOrderId = nextOrderId;
        Instruments = instruments;
        Prices = prices;
        ComputerPortfolios = computerPortfolios;
    }

    public int Turn { get; }
    public Player Player { get; }
    public OrderBook OrderBook { get; }
    public int NextOrderId { get; }
    public IReadOnlyList<Instrument> Instruments { get; }
    public IReadOnlyDictionary<int, int> Prices { get; }
    public IReadOnlyDictionary<string, Portfolio> ComputerPortfolios { get; }

    private static IReadOnlyDictionary<string, Portfolio> CreateInitialComputerPortfolios()
    {
        var dict = new Dictionary<string, Portfolio>(GameRules.ComputerTraders.Count);
        for (int i = 1; i <= GameRules.ComputerTraders.Count; i++)
            dict[$"{ComputerTrader.TraderIdPrefix}{i}"] = Portfolio.CreateInfinite();
        return dict;
    }
}
