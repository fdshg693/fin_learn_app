namespace MyApp.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
/// </summary>
public sealed class TurnProcessor
{
    public IOrderPlacer OrderPlacer { get; }
    public IMarket Market { get; }

    public TurnProcessor(IOrderPlacer orderPlacer)
        : this(orderPlacer, new Market())
    {
    }

    public TurnProcessor(IOrderPlacer orderPlacer, IMarket market)
    {
        OrderPlacer = orderPlacer;
        Market = market;
    }

    public (Game Result, string? Warning) Buy(Game game, IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);

        var instrument = new Instrument(instrumentId);
        var price = exchange.PriceOf(instrumentId);
        return PlaceOrder(game, exchange, instrument, OrderSide.Buy, quantity, price, Messages.NoMatchingSellOrders);
    }

    public (Game Result, string? Warning) Sell(Game game, IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);

        if (game.Player.Portfolio.QuantityOf(instrumentId) < quantity)
            return (game, Messages.InsufficientQuantityToSell);

        var instrument = new Instrument(instrumentId);
        // 成行注文: price=1で全買い注文とマッチ可能
        return PlaceOrder(game, exchange, instrument, OrderSide.Sell, quantity, price: 1, Messages.NoMatchingBuyOrders);
    }

    public (Game Result, string? Warning) Wait(Game game, IExchange exchange)
    {
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);
        return (new Game(game.Player, game.Turn + 1, bookWithOrders, nextId, game.Instruments), null);
    }

    private (Game Result, string? Warning) PlaceOrder(
        Game game, IExchange exchange, Instrument instrument, OrderSide side,
        int quantity, int price, string noMatchMessage)
    {
        // 1. コンピューター注文を生成
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);

        // 2. プレイヤーの注文を生成
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price);

        // 3. 市場で約定
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        if (matchResult.Trade.FilledQuantity == 0)
            return (game, noMatchMessage);

        // 4. プレイヤーのポートフォリオを更新
        var (resultPlayer, warning) = game.Player.ApplyTrade(matchResult.Trade);
        if (warning is not null)
            return (game, warning);

        return (new Game(resultPlayer, game.Turn + 1, matchResult.UpdatedBook,
            nextId + 1, game.Instruments), null);
    }
}
