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

        var instrument = new Instrument(instrumentId);
        var price = exchange.PriceOf(instrumentId);
        return PlaceOrder(exchange, instrument, OrderSide.Buy, quantity, price, Messages.NoMatchingSellOrders);
    }

    public (Game Result, string? Warning) Sell(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (this, Messages.QuantityMustBePositive);

        if (Player.Portfolio.QuantityOf(instrumentId) < quantity)
            return (this, Messages.InsufficientQuantityToSell);

        var instrument = new Instrument(instrumentId);
        // 成行注文: price=1で全買い注文とマッチ可能
        return PlaceOrder(exchange, instrument, OrderSide.Sell, quantity, price: 1, Messages.NoMatchingBuyOrders);
    }

    private (Game Result, string? Warning) PlaceOrder(
        IExchange exchange, Instrument instrument, OrderSide side,
        int quantity, int price, string noMatchMessage)
    {
        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(OrderBook, exchange, Instruments, NextOrderId);

        // 2. プレイヤーの注文を生成
        var order = Player.CreateOrder(nextId, instrument, side, quantity, price);

        // 3. 市場で約定
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        if (matchResult.Trade.FilledQuantity == 0)
            return (this, noMatchMessage);

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
