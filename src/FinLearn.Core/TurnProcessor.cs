namespace FinLearn.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
/// </summary>
public sealed class TurnProcessor
{
    public IOrderPlacer OrderPlacer { get; }
    public IMarket Market { get; }
    public IPriceFluctuator Fluctuator { get; }
    public IExchangeFactory ExchangeFactory { get; }

    public TurnProcessor(IOrderPlacer orderPlacer, IPriceFluctuator fluctuator)
        : this(orderPlacer, new Market(), fluctuator, new SimpleExchangeFactory())
    {
    }

    public TurnProcessor(IOrderPlacer orderPlacer, IMarket market,
        IPriceFluctuator fluctuator, IExchangeFactory exchangeFactory)
    {
        OrderPlacer = orderPlacer;
        Market = market;
        Fluctuator = fluctuator;
        ExchangeFactory = exchangeFactory;
    }

    public (Game Result, string? Warning) Buy(Game game, int fee, int instrumentId, int quantity, int? price = null)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return (game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Buy, quantity, price, Messages.NoMatchingSellOrders);
    }

    public (Game Result, string? Warning) Sell(Game game, int fee, int instrumentId, int quantity, int? price = null)
    {
        if (quantity <= 0)
            return (game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return (game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Sell, quantity, price, Messages.NoMatchingBuyOrders);
    }

    public (Game Result, string? Warning) Wait(Game game, int fee)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);
        var newPrices = Fluctuator.Fluctuate(game.Prices);
        return (new Game(game.Player, game.Turn + 1, bookWithOrders, nextId, game.Instruments, newPrices), null);
    }

    private (Game Result, string? Warning) PlaceOrder(
        Game game, int fee, Instrument instrument, OrderSide side,
        int quantity, int? price, string noMatchMessage)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);

        // 1. コンピューター注文を生成 → プレイヤー注文を生成 → 市場で約定
        var (bookWithOrders, nextId) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId);
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price);
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        // 2. 成行注文で約定ゼロ → コンピューター注文は板に残し、Waitと同じ挙動でターンを進める
        if (price is null && matchResult.Trade.FilledQuantity == 0)
            return (AdvanceTurn(game, game.Player, bookWithOrders, nextId), noMatchMessage);

        // 3. 約定分があればポートフォリオを更新
        var (resultPlayer, warning) = ApplyTradeToPlayer(game.Player, matchResult.Trade);
        if (warning is not null)
            return (AdvanceTurn(game, game.Player, bookWithOrders, nextId), warning);

        // 4. 指値注文の未約定分を板に追加
        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, quantity, matchResult.Trade.FilledQuantity, price);

        // 5. 株価変動を適用して新しいゲーム状態を返す
        return (AdvanceTurn(game, resultPlayer, updatedBook, nextId + 1), null);
    }

    private static (Player Result, string? Warning) ApplyTradeToPlayer(Player player, TradeResult trade)
    {
        if (trade.FilledQuantity <= 0)
            return (player, null);

        var (resultPortfolio, warning) = player.Portfolio.ApplyTrade(trade);
        if (warning is not null)
            return (player, warning);

        return (player.WithPortfolio(resultPortfolio), null);
    }

    private static OrderBook AddRemainingLimitOrder(OrderBook book, Order order, int requestedQty, int filledQty, int? price)
    {
        if (price is null)
            return book;

        var remainingQty = requestedQty - filledQty;
        if (remainingQty <= 0)
            return book;

        return book.Add(order.WithQuantity(remainingQty));
    }

    private Game AdvanceTurn(Game game, Player player, OrderBook book, int nextOrderId)
    {
        var newPrices = Fluctuator.Fluctuate(game.Prices);
        return new Game(player, game.Turn + 1, book, nextOrderId, game.Instruments, newPrices);
    }
}
