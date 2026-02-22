namespace MyApp.Core;

public sealed class Game
{
    public Game(IOrderPlacer orderPlacer, IReadOnlyList<Instrument> instruments)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1,
               orderPlacer, instruments, new Market())
    {
    }

    public Game(IOrderPlacer orderPlacer, IReadOnlyList<Instrument> instruments, IMarket market)
        : this(new Player(), turn: 1, new OrderBook(), nextOrderId: 1,
               orderPlacer, instruments, market)
    {
    }

    private Game(Player player, int turn, OrderBook orderBook, int nextOrderId,
        IOrderPlacer orderPlacer, IReadOnlyList<Instrument> instruments, IMarket market)
    {
        Player = player;
        Turn = turn;
        OrderBook = orderBook;
        NextOrderId = nextOrderId;
        OrderPlacer = orderPlacer;
        Instruments = instruments;
        Market = market;
    }

    public int Turn { get; }
    public Player Player { get; }
    public OrderBook OrderBook { get; }
    public int NextOrderId { get; }
    public IOrderPlacer OrderPlacer { get; }
    public IReadOnlyList<Instrument> Instruments { get; }
    public IMarket Market { get; }

    public (Game Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (this, Messages.QuantityMustBePositive);

        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);

        // 2. プレイヤーの買い注文を生成
        var instrument = new Instrument(instrumentId);
        var buyPrice = exchange.PriceOf(instrumentId);
        var order = Player.CreateBuyOrder(nextId, instrument, quantity, buyPrice);

        // 3. 市場で約定
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        if (matchResult.Trade.FilledQuantity == 0)
            return (this, Messages.NoMatchingSellOrders);

        // 4. プレイヤーのポートフォリオを更新
        var (resultPlayer, warning) = Player.ApplyTrade(matchResult.Trade);
        if (warning is not null)
            return (this, warning);

        return (new Game(resultPlayer, Turn + 1, matchResult.UpdatedBook,
            nextId + 1, OrderPlacer, Instruments, Market), null);
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

        // 2. プレイヤーの売り注文を生成（成行注文: price=1で全買い注文とマッチ可能）
        var instrument = new Instrument(instrumentId);
        var order = Player.CreateSellOrder(nextId, instrument, quantity, price: 1);

        // 3. 市場で約定
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        if (matchResult.Trade.FilledQuantity == 0)
            return (this, Messages.NoMatchingBuyOrders);

        // 4. プレイヤーのポートフォリオを更新
        var (resultPlayer, warning) = Player.ApplyTrade(matchResult.Trade);
        if (warning is not null)
            return (this, warning);

        return (new Game(resultPlayer, Turn + 1, matchResult.UpdatedBook,
            nextId + 1, OrderPlacer, Instruments, Market), null);
    }

    public (Game Result, string? Warning) Wait(IExchange exchange)
    {
        // コンピューター注文を生成してからターンを進める
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);
        return (new Game(Player, Turn + 1, bookWithOrders, nextId,
            OrderPlacer, Instruments, Market), null);
    }
}
