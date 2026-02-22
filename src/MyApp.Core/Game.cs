namespace MyApp.Core;

/// <summary>
/// ゲームの状態スナップショット。ワークフロー制御は TurnProcessor が担う。
/// </summary>
public sealed class Game
{
    public Game(IReadOnlyList<Instrument> instruments)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1, instruments)
    {
    }

    public Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IReadOnlyList<Instrument> instruments)
    {
        Player = player;
        Turn = turn;
        OrderBook = orderBook;
        NextOrderId = nextOrderId;
        Instruments = instruments;
    }

    public int Turn { get; }
    public Player Player { get; }
    public OrderBook OrderBook { get; }
    public int NextOrderId { get; }
    public IReadOnlyList<Instrument> Instruments { get; }
}
