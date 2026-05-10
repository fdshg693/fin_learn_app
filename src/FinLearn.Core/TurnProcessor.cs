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

    /// <summary>
    /// 買い注文を発行してターンを進める。
    /// 引数バリデーション失敗時は SubmittedOrders / Fills が空、Warning が設定される。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Buy(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);
        if (expiresInTurns <= 0)
            return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Buy, quantity, price, stopPrice, expiresInTurns, Messages.NoMatchingSellOrders);
    }

    /// <summary>
    /// 売り注文を発行してターンを進める。
    /// 引数バリデーション失敗時は SubmittedOrders / Fills が空、Warning が設定される。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Sell(Game game, int fee, int instrumentId, int quantity, int? price = null, int? stopPrice = null, int expiresInTurns = GameRules.DefaultOrderTtl)
    {
        if (quantity <= 0)
            return Rejected(game, Messages.QuantityMustBePositive);
        if (price is not null && price <= 0)
            return Rejected(game, Messages.PriceMustBePositive);
        if (expiresInTurns <= 0)
            return Rejected(game, Messages.ExpiresInTurnsMustBePositive);

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Sell, quantity, price, stopPrice, expiresInTurns, Messages.NoMatchingBuyOrders);
    }

    /// <summary>
    /// プレイヤー注文を発行せずに 1 ターン待機する。
    /// SubmittedOrders はコンピューター注文のみ、Fills は空、Warning は常に null。
    /// 詳細は <see cref="TurnResult"/> を参照。
    /// </summary>
    public TurnResult Wait(Game game, int fee)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var (bookWithOrders, nextId, placedOrders, computerPortfolios) =
            OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn, game.ComputerPortfolios);

        var nextGame = AdvanceTurn(game, game.Player, bookWithOrders, nextId, computerPortfolios);
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
        int quantity, int? price, int? stopPrice, int expiresInTurns, string noMatchMessage)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);

        // 1. コンピューター注文を生成 → プレイヤー注文を生成 → 市場で約定
        var (bookWithOrders, nextId, placedOrders, computerPortfolios) =
            OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn, game.ComputerPortfolios);
        var expiresAtTurn = game.Turn + expiresInTurns;
        var order = game.Player.CreateOrder(nextId, instrument, side, quantity, price, stopPrice, game.Turn, expiresAtTurn);
        var matchResult = Market.Execute(bookWithOrders, order, exchange);

        // プレイヤー注文が板上の computer の resting order と約定した分を、その computer の Portfolio に反映
        var updatedComputerPortfolios = ApplyMatchToComputerPortfolios(
            matchResult.Fills, bookWithOrders, computerPortfolios, exchange.Fee, excludeOrderId: order.Id);

        var submittedOrders = Combine(placedOrders, order);

        // 2. 成行注文で約定ゼロ → コンピューター注文は板に残し、Waitと同じ挙動でターンを進める
        if (price is null && matchResult.Trade.FilledQuantity == 0)
        {
            var nextGameNoMatch = AdvanceTurn(game, game.Player, bookWithOrders, nextId, updatedComputerPortfolios);
            return new TurnResult(nextGameNoMatch, null, noMatchMessage, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 3. 約定分があればポートフォリオを更新
        var (resultPlayer, warning) = ApplyTradeToPlayer(game.Player, matchResult.Trade);
        if (warning is not null)
        {
            // 残高不足等で約定をロールバック → Fills は空にしてログ＝確定事実の対応関係を保つ
            var rolledBack = AdvanceTurn(game, game.Player, bookWithOrders, nextId, computerPortfolios);
            return new TurnResult(rolledBack, null, warning, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 4. 指値注文の未約定分を板に追加
        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, quantity, matchResult.Trade.FilledQuantity, price);

        // 5. 株価変動を適用して新しいゲーム状態を返す
        var nextGame = AdvanceTurn(game, resultPlayer, updatedBook, nextId + 1, updatedComputerPortfolios);
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

    /// <summary>
    /// プレイヤー注文の Match 結果のうち、板側の computer resting order に対する各 OrderFill を
    /// その computer の Portfolio に反映する。incoming のプレイヤー注文（excludeOrderId）は除外。
    /// </summary>
    private static IReadOnlyDictionary<string, Portfolio> ApplyMatchToComputerPortfolios(
        IReadOnlyList<OrderFill> fills,
        OrderBook preMatchBook,
        IReadOnlyDictionary<string, Portfolio> computerPortfolios,
        int fee,
        int excludeOrderId)
    {
        if (fills.Count == 0)
            return computerPortfolios;

        var ordersById = new Dictionary<int, Order>();
        foreach (var o in preMatchBook.Orders)
            ordersById[o.Id] = o;

        var portfolios = new Dictionary<string, Portfolio>(computerPortfolios);
        foreach (var fill in fills)
        {
            if (fill.OrderId == excludeOrderId) continue;
            if (fill.FilledQuantity <= 0) continue;
            if (!ordersById.TryGetValue(fill.OrderId, out var restingOrder)) continue;
            if (!ComputerTrader.IsComputerTrader(restingOrder.TraderId)) continue;
            if (!portfolios.TryGetValue(restingOrder.TraderId, out var portfolio)) continue;

            var trade = new TradeResult(
                InstrumentId: restingOrder.Instrument.Id,
                Side: restingOrder.Side,
                FilledQuantity: fill.FilledQuantity,
                TotalAmount: fill.TotalAmount,
                Fee: fee);
            var (updated, _) = portfolio.ApplyTrade(trade);
            portfolios[restingOrder.TraderId] = updated;
        }

        return portfolios;
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

    private Game AdvanceTurn(Game game, Player player, OrderBook book, int nextOrderId,
        IReadOnlyDictionary<string, Portfolio> computerPortfolios)
    {
        var newPrices = Fluctuator.Fluctuate(game.Prices);
        var expiredBook = book.ExpireOrders(game.Turn + 1);
        return new Game(player, game.Turn + 1, expiredBook, nextOrderId, game.Instruments, newPrices, computerPortfolios);
    }
}
