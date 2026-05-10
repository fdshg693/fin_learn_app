namespace FinLearn.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
///
/// Buy/Sell/Wait は共通 pipeline <c>RunTurn</c> に集約され、
/// <see cref="World"/> snapshot を <see cref="IPlayerOrderHandler"/>
/// (<see cref="LimitOrderHandler"/> / <see cref="MarketOrderHandler"/>) で遷移させる。
/// Pipeline は Computer → Receive → Match → Settle → BookUpdate → TurnAdvance の各 phase で構成。
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

    /// <summary>
    /// 買い注文を発行してターンを進める。
    /// 引数バリデーション失敗時は SubmittedOrders / Fills が空、Warning が設定される。
    /// </summary>
    public TurnResult Buy(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);
        if (expiresInTurns <= 0)
            return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

        return RunTurn(game, fee,
            handler: SelectHandler(price),
            intentFactory: nextOrderId => game.Player.CreateOrder(
                nextOrderId, new Instrument(instrumentId), OrderSide.Buy,
                quantity, price, stopPrice, game.Turn, game.Turn + expiresInTurns));
    }

    /// <summary>
    /// 売り注文を発行してターンを進める。
    /// </summary>
    public TurnResult Sell(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);
        if (expiresInTurns <= 0)
            return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

        return RunTurn(game, fee,
            handler: SelectHandler(price),
            intentFactory: nextOrderId => game.Player.CreateOrder(
                nextOrderId, new Instrument(instrumentId), OrderSide.Sell,
                quantity, price, stopPrice, game.Turn, game.Turn + expiresInTurns));
    }

    /// <summary>
    /// プレイヤー注文を発行せずに 1 ターン待機する。
    /// </summary>
    public TurnResult Wait(Game game, int fee) =>
        RunTurn(game, fee, handler: null, intentFactory: null);

    private static TurnResult Rejected(Game game, string warning) =>
        new(game, null, warning, game.Turn, Array.Empty<Order>(), Array.Empty<OrderFill>());

    private static IReadOnlyList<Order> Combine(IReadOnlyList<Order> placedOrders, Order playerOrder)
    {
        var combined = new List<Order>(placedOrders.Count + 1);
        combined.AddRange(placedOrders);
        combined.Add(playerOrder);
        return combined;
    }

    private static OrderBook AddRemainingLimitOrder(OrderBook book, Order order, int filledQty)
    {
        if (order.Price is null)
            return book;

        var remainingQty = order.Quantity - filledQty;
        if (remainingQty <= 0)
            return book;

        return book.Add(order.WithQuantity(remainingQty));
    }

    /// <summary>
    /// Buy/Sell/Wait 共通の pipeline。
    /// intentFactory が null = Wait、それ以外は Buy/Sell。
    /// </summary>
    private TurnResult RunTurn(Game game, int fee,
        IPlayerOrderHandler? handler, Func<int, Order>? intentFactory)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var world = World.FromGame(game, fee, exchange);

        // [Phase: Computer] computer 注文発注 + 約定 + settlement
        var placement = OrderPlacer.PlaceOrders(
            world.Book, exchange, world.Instruments, world.NextOrderId, world.Turn, world.Portfolios);
        world = world
            .WithBook(placement.UpdatedBook)
            .WithPortfolios(placement.UpdatedTraderPortfolios)
            .WithNextOrderId(placement.NextOrderId);

        // [Phase: Player Intent] Order 作成 (Wait なら null)
        var order = intentFactory?.Invoke(world.NextOrderId);
        var submittedOrders = order is null
            ? placement.PlacedOrders
            : Combine(placement.PlacedOrders, order);

        if (order is null || handler is null)
            return BuildTurnResult(game, world, trade: null, warning: null,
                submittedOrders, fills: Array.Empty<OrderFill>());

        world = world.WithNextOrderId(world.NextOrderId + 1);

        // [Phase: Receive] 限値: 予約 / 成行: no-op
        var (afterReceive, receiveWarn) = handler.Receive(world, order);
        if (receiveWarn is not null)
            return BuildTurnResult(game, world, trade: null, warning: receiveWarn,
                submittedOrders, fills: Array.Empty<OrderFill>());
        world = afterReceive;

        // [Phase: Match] pipeline 共通
        var match = Market.Execute(world.Book, order, exchange);

        // [Phase: Settle] 結果反映 (失敗時は match を捨てる = world.Book を変えない)
        var (afterSettle, settleWarn) = handler.Settle(world, order, match);
        if (settleWarn is not null)
            return BuildTurnResult(game, world, trade: null, warning: settleWarn,
                submittedOrders, fills: Array.Empty<OrderFill>());
        world = afterSettle;

        // [Phase: BookUpdate] match 結果と限値残量で板を確定
        var finalBook = AddRemainingLimitOrder(match.UpdatedBook, order, match.Trade.FilledQuantity);
        world = world.WithBook(finalBook);

        return BuildTurnResult(game, world, trade: match.Trade, warning: null,
            submittedOrders, fills: match.Fills);
    }

    /// <summary>
    /// World を Game に書き戻し、AdvanceTurn してから TurnResult を組み立てる。
    /// </summary>
    private TurnResult BuildTurnResult(Game inputGame, World world,
        TradeResult? trade, string? warning,
        IReadOnlyList<Order> submittedOrders, IReadOnlyList<OrderFill> fills)
    {
        var nextGame = AdvanceTurn(inputGame, world);
        return new TurnResult(
            Game: nextGame,
            Trade: trade,
            Warning: warning,
            ProcessedTurn: inputGame.Turn,
            SubmittedOrders: submittedOrders,
            Fills: fills);
    }

    /// <summary>
    /// 価格変動 + 失効処理 + 失効注文の予約解放 + Player/ComputerPortfolios 分解。
    /// </summary>
    private Game AdvanceTurn(Game inputGame, World world)
    {
        var newPrices = Fluctuator.Fluctuate(world.Prices);
        var (expiredBook, expired) = world.Book.ExpireOrders(world.Turn + 1);
        var afterRelease = SettlementProcessor.ReleaseExpired(expired, world.Portfolios, world.Fee);

        var (player, computers) = SplitPortfolios(inputGame.Player, afterRelease, world.PlayerName);
        return new Game(
            player, world.Turn + 1, expiredBook, world.NextOrderId,
            world.Instruments, newPrices, computers);
    }

    private static (Player Player, IReadOnlyDictionary<string, Portfolio> ComputerPortfolios)
        SplitPortfolios(Player original, IReadOnlyDictionary<string, Portfolio> all, string playerName)
    {
        var newPlayer = original.WithPortfolio(all[playerName]);
        var computers = new Dictionary<string, Portfolio>(all.Count - 1);
        foreach (var (id, pf) in all)
            if (id != playerName) computers[id] = pf;
        return (newPlayer, computers);
    }

    private static IPlayerOrderHandler SelectHandler(int? price) =>
        price is null ? new MarketOrderHandler() : new LimitOrderHandler();
}
