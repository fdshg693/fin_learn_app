namespace MyApp.Core;

/// <summary>
/// ゲームの状態スナップショット。ワークフロー制御は TurnProcessor が担う。
/// </summary>
public sealed class Game
{
    public Game(IReadOnlyList<Instrument> instruments, IReadOnlyDictionary<int, int> prices)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1, instruments, prices)
    {
    }

    public Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IReadOnlyList<Instrument> instruments, IReadOnlyDictionary<int, int> prices)
    {
        Player = player;
        Turn = turn;
        OrderBook = orderBook;
        NextOrderId = nextOrderId;
        Instruments = instruments;
        Prices = prices;
    }

    public int Turn { get; }
    public Player Player { get; }
    public OrderBook OrderBook { get; }
    public int NextOrderId { get; }
    public IReadOnlyList<Instrument> Instruments { get; }
    public IReadOnlyDictionary<int, int> Prices { get; }
}
