namespace MyApp.Core;

public sealed class Game
{
    public Game(IOrderPlacer orderPlacer, IReadOnlyList<Instrument> instruments)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1, orderPlacer, instruments)
    {
    }

    private Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IOrderPlacer orderPlacer, IReadOnlyList<Instrument> instruments)
    {
        Player = player;
        Turn = turn;
        OrderBook = orderBook;
        NextOrderId = nextOrderId;
        OrderPlacer = orderPlacer;
        Instruments = instruments;
    }

    public int Turn { get; }
    public Player Player { get; }
    public OrderBook OrderBook { get; }
    public int NextOrderId { get; }
    public IOrderPlacer OrderPlacer { get; }
    public IReadOnlyList<Instrument> Instruments { get; }

    public (Game Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (this, Messages.QuantityMustBePositive);

        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);

        // 2. 注文帳で約定（プレイヤーの買い注文 vs コンピューターの売り注文）
        var buyPrice = exchange.PriceOf(instrumentId);
        var fillResult = bookWithOrders.FillBuy(instrumentId, quantity, buyPrice);

        if (fillResult.FilledQuantity == 0)
            return (this, Messages.NoMatchingSellOrders);

        // 3. プレイヤーのポートフォリオを更新
        var (resultPlayer, warning) = Player.Buy(
            instrumentId, fillResult.FilledQuantity, fillResult.TotalAmount, exchange.Fee);
        if (warning is not null)
            return (this, warning);

        return (new Game(resultPlayer, Turn + 1, fillResult.UpdatedBook, nextId, OrderPlacer, Instruments), null);
    }

    public (Game Result, string? Warning) Sell(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (this, Messages.QuantityMustBePositive);

        // 保有数チェック
        if (Player.Portfolio.QuantityOf(instrumentId) < quantity)
            return (this, Messages.InsufficientQuantityToSell);

        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);

        // 2. 最高買い注文価格を取得して売り約定
        var buyOrders = bookWithOrders.BuyOrders(instrumentId);
        if (buyOrders.Count == 0)
            return (this, Messages.NoMatchingBuyOrders);

        var bestBuyPrice = buyOrders[0].Price; // 降順ソート済み
        var fillResult = bookWithOrders.FillSell(instrumentId, quantity, bestBuyPrice);

        if (fillResult.FilledQuantity == 0)
            return (this, Messages.NoMatchingBuyOrders);

        // 3. プレイヤーのポートフォリオを更新
        var (resultPlayer, warning) = Player.Sell(
            instrumentId, fillResult.FilledQuantity, fillResult.TotalAmount, exchange.Fee);
        if (warning is not null)
            return (this, warning);

        return (new Game(resultPlayer, Turn + 1, fillResult.UpdatedBook, nextId, OrderPlacer, Instruments), null);
    }

    public (Game Result, string? Warning) Wait(IExchange exchange)
    {
        // コンピューター注文を生成してからターンを進める
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);
        return (new Game(Player, Turn + 1, bookWithOrders, nextId, OrderPlacer, Instruments), null);
    }
}
