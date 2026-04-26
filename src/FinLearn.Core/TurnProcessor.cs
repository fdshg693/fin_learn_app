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
    public int ComputerTtl { get; }
    public int PlayerTtl { get; }

    public TurnProcessor(IOrderPlacer orderPlacer, IPriceFluctuator fluctuator,
        int computerTtl = int.MaxValue, int playerTtl = int.MaxValue)
        : this(orderPlacer, new Market(), fluctuator, new SimpleExchangeFactory(), computerTtl, playerTtl)
    {
    }

    public TurnProcessor(IOrderPlacer orderPlacer, IMarket market,
        IPriceFluctuator fluctuator, IExchangeFactory exchangeFactory,
        int computerTtl = int.MaxValue, int playerTtl = int.MaxValue)
    {
        OrderPlacer = orderPlacer;
        Market = market;
        Fluctuator = fluctuator;
        ExchangeFactory = exchangeFactory;
        ComputerTtl = computerTtl;
        PlayerTtl = playerTtl;
    }

    /// <summary>
    /// 買い注文を発行してターンを進める。
    /// 引数バリデーション失敗時は SubmittedOrders / Fills が空、Warning が設定される。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Buy(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Buy, quantity, price, stopPrice, Messages.NoMatchingSellOrders);
    }

    /// <summary>
    /// 売り注文を発行してターンを進める。
    /// 引数バリデーション失敗時は SubmittedOrders / Fills が空、Warning が設定される。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Sell(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Sell, quantity, price, stopPrice, Messages.NoMatchingBuyOrders);
    }

    /// <summary>
    /// プレイヤー注文を発行せずに 1 ターン待機する。
    /// SubmittedOrders はコンピューター注文のみ、Fills は空、Warning は常に null。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Wait(Game game, int fee)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var (bookWithOrders, nextId, placedOrders) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);

        var nextGame = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
        return new TurnResult(
            Game: nextGame,
            Trade: null,
            Warning: null,
            ProcessedTurn: game.Turn,
            SubmittedOrders: placedOrders,
            Fills: Array.Empty<OrderFill>());
    }

    private TurnResult PlaceOrder(
        Game game, int fee, Instrument instrument, OrderSide side,
        int quantity, int? price, int? stopPrice, string noMatchMessage)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);

        // 1. コンピューター注文を生成 → プレイヤー注文を生成 → 市場で約定
        var (bookWithOrders, nextId, placedOrders) = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn);
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price, stopPrice, game.Turn);
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        var submittedOrders = Combine(placedOrders, order);

        // 2. 成行注文で約定ゼロ → コンピューター注文は板に残し、Waitと同じ挙動でターンを進める
        if (price is null && matchResult.Trade.FilledQuantity == 0)
        {
            var nextGameNoMatch = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
            return new TurnResult(nextGameNoMatch, null, noMatchMessage, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 3. 約定分があればポートフォリオを更新
        var (resultPlayer, warning) = ApplyTradeToPlayer(game.Player, matchResult.Trade);
        if (warning is not null)
        {
            // 残高不足等で約定をロールバック → Fills は空にしてログ＝確定事実の対応関係を保つ
            var rolledBack = AdvanceTurn(game, game.Player, bookWithOrders, nextId);
            return new TurnResult(rolledBack, null, warning, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 4. 指値注文の未約定分を板に追加
        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, quantity, matchResult.Trade.FilledQuantity, price);

        // 5. 株価変動を適用して新しいゲーム状態を返す
        var nextGame = AdvanceTurn(game, resultPlayer, updatedBook, nextId + 1);
        return new TurnResult(nextGame, matchResult.Trade, null, game.Turn, submittedOrders, matchResult.Fills);
    }

    private static TurnResult Rejected(Game game, string warning) =>
        new(game, null, warning, game.Turn, Array.Empty<Order>(), Array.Empty<OrderFill>());

    private static IReadOnlyList<Order> Combine(IReadOnlyList<Order> placedOrders, Order playerOrder)
    {
        var combined = new List<Order>(placedOrders.Count + 1);
        combined.AddRange(placedOrders);
        combined.Add(playerOrder);
        return combined;
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
        var expiredBook = book.ExpireOrders(game.Turn + 1, ComputerTtl, PlayerTtl);
        return new Game(player, game.Turn + 1, expiredBook, nextOrderId, game.Instruments, newPrices);
    }
}
