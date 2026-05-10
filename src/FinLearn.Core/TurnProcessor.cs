namespace FinLearn.Core;

/// <summary>
/// ターン進行のワークフローを担うドメインサービス。
/// Game は状態スナップショットに徹し、TurnProcessor がアクション処理を行う。
///
/// 注文生成（intent）と settlement（マーケット結果反映）の責務を分離する設計:
/// - <see cref="ComputerTrader"/> が computer 注文の発注 + 約定 + 自身の settlement を完結させる
///   （player の resting 注文と約定したケースも <see cref="SettlementProcessor"/> 経由で player Portfolio に反映）
/// - 本クラスは player 注文の発注時に予約（指値）/同期約定（成行）を行う
/// - 失効注文の予約解放も <see cref="SettlementProcessor.ReleaseExpired"/> で統一
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

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Buy, quantity, price, stopPrice, expiresInTurns, Messages.NoMatchingSellOrders);
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

        var instrument = new Instrument(instrumentId);
        return PlaceOrder(game, fee, instrument, OrderSide.Sell, quantity, price, stopPrice, expiresInTurns, Messages.NoMatchingBuyOrders);
    }

    /// <summary>
    /// プレイヤー注文を発行せずに 1 ターン待機する。
    /// </summary>
    public TurnResult Wait(Game game, int fee)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);
        var allPortfolios = BuildAllPortfolios(game);
        var placement = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn, allPortfolios);

        var nextGame = AdvanceTurn(game, placement.UpdatedBook, placement.NextOrderId, placement.UpdatedTraderPortfolios, fee);
        return new TurnResult(
            Game: nextGame,
            Trade: null,
            Warning: null,
            ProcessedTurn: game.Turn,
            SubmittedOrders: placement.PlacedOrders,
            Fills: Array.Empty<OrderFill>());
    }

    private TurnResult PlaceOrder(
        Game game, int fee, Instrument instrument, OrderSide side,
        int quantity, int? price, int? stopPrice, int expiresInTurns, string noMatchMessage)
    {
        var exchange = ExchangeFactory.Create(game.Prices, fee);

        // 1. 統合 Portfolio map を構築
        var allPortfolios = BuildAllPortfolios(game);

        // 2. computer 注文の発注 + 約定 + settlement（player resting への約定も含む）
        var placement = OrderPlacer.PlaceOrders(game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn, allPortfolios);
        allPortfolios = new Dictionary<string, Portfolio>(placement.UpdatedTraderPortfolios);

        // 3. プレイヤー注文を作成
        var expiresAtTurn = game.Turn + expiresInTurns;
        var order = game.Player.CreateOrder(placement.NextOrderId, instrument, side, quantity, price, stopPrice, game.Turn, expiresAtTurn);
        var submittedOrders = Combine(placement.PlacedOrders, order);

        // 4. 指値の場合のみ事前予約（available チェック兼ねる）
        if (price is not null)
        {
            var playerPf = allPortfolios[game.Player.Name];
            var (reserved, reserveWarn) = side == OrderSide.Buy
                ? playerPf.ReserveBuy(instrument.Id, quantity, price.Value, fee)
                : playerPf.ReserveSell(instrument.Id, quantity);
            if (reserveWarn is not null)
            {
                // 予約失敗（残高/数量不足）→ Wait 化、computer 注文の settlement は確定維持。
                // SubmittedOrders は player 注文も含む（旧来挙動）。
                var rejectedGame = AdvanceTurn(game, placement.UpdatedBook, placement.NextOrderId + 1, allPortfolios, fee);
                return new TurnResult(rejectedGame, null, reserveWarn, game.Turn, submittedOrders, Array.Empty<OrderFill>());
            }
            allPortfolios[game.Player.Name] = reserved;
        }

        // 5. プレイヤー注文をマッチング
        var matchResult = Market.Execute(placement.UpdatedBook, order, exchange);

        // 6. 成行で約定ゼロ → Wait と同じ進行
        if (price is null && matchResult.Trade.FilledQuantity == 0)
        {
            var nextGameNoMatch = AdvanceTurn(game, placement.UpdatedBook, placement.NextOrderId + 1, allPortfolios, fee);
            return new TurnResult(nextGameNoMatch, null, noMatchMessage, game.Turn, submittedOrders, Array.Empty<OrderFill>());
        }

        // 7. プレイヤー注文の fills を settlement に通す
        if (price is null)
        {
            // 成行: 同期 ApplyTrade で player Portfolio に適用。残高不足ならロールバック。
            var trade = matchResult.Trade;
            var playerPf = allPortfolios[game.Player.Name];
            var (afterTrade, applyWarn) = playerPf.ApplyTrade(trade);
            if (applyWarn is not null)
            {
                // ロールバック: matchResult.UpdatedBook は捨てて placement.UpdatedBook を使う。
                // computer settlement（placement 内で適用済み）は確定維持する。
                var rolledBack = AdvanceTurn(game, placement.UpdatedBook, placement.NextOrderId + 1, allPortfolios, fee);
                return new TurnResult(rolledBack, null, applyWarn, game.Turn, submittedOrders, Array.Empty<OrderFill>());
            }
            allPortfolios[game.Player.Name] = afterTrade;
        }
        else
        {
            // 指値: SettlementProcessor で予約消費 + 差額返金。失敗パスは無い。
            var ordersById = BuildOrdersByIdSnapshot(placement.UpdatedBook, order);
            var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(matchResult.Fills, ordersById);
            var settled = SettlementProcessor.SettleFills(matchResult.Fills, ordersById, postFillRemaining, allPortfolios, fee);
            allPortfolios = new Dictionary<string, Portfolio>(settled);
        }

        // 8. 指値の未約定分を板に追加
        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, quantity, matchResult.Trade.FilledQuantity, price);

        // 9. ターン進行（株価変動 + 失効処理 + 失効注文の予約解放）
        var nextGame = AdvanceTurn(game, updatedBook, placement.NextOrderId + 1, allPortfolios, fee);
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

    private static OrderBook AddRemainingLimitOrder(OrderBook book, Order order, int requestedQty, int filledQty, int? price)
    {
        if (price is null)
            return book;

        var remainingQty = requestedQty - filledQty;
        if (remainingQty <= 0)
            return book;

        return book.Add(order.WithQuantity(remainingQty));
    }

    /// <summary>
    /// Player.Portfolio + Game.ComputerPortfolios を統合して traderId キーの map を作る。
    /// Settlement 用の一時 view。最終的に <see cref="SplitPortfolios"/> で Game に書き戻す。
    /// </summary>
    private static Dictionary<string, Portfolio> BuildAllPortfolios(Game game)
    {
        var dict = new Dictionary<string, Portfolio>(game.ComputerPortfolios.Count + 1);
        foreach (var (id, pf) in game.ComputerPortfolios)
            dict[id] = pf;
        dict[game.Player.Name] = game.Player.Portfolio;
        return dict;
    }

    /// <summary>
    /// 統合 map を Player と ComputerPortfolios に分解する。
    /// </summary>
    private static (Player Player, IReadOnlyDictionary<string, Portfolio> ComputerPortfolios) SplitPortfolios(
        Player original, IReadOnlyDictionary<string, Portfolio> all)
    {
        var newPlayer = original.WithPortfolio(all[original.Name]);
        var computers = new Dictionary<string, Portfolio>(all.Count - 1);
        foreach (var (id, pf) in all)
        {
            if (id == original.Name) continue;
            computers[id] = pf;
        }
        return (newPlayer, computers);
    }

    /// <summary>
    /// fill 逆引き用に「post-match の板に乗っている orders + プレイヤー注文」のスナップショットを作る。
    /// 部分約定後の resting orders は WithQuantity で減算済 Quantity を持つ。
    /// プレイヤー incoming 注文は元の発注数量で含める（settlement の postFillRemaining 計算に必要）。
    /// </summary>
    private static IReadOnlyDictionary<int, Order> BuildOrdersByIdSnapshot(OrderBook postMatchBook, Order playerOrder)
    {
        var dict = new Dictionary<int, Order>();
        foreach (var o in postMatchBook.Orders)
            dict[o.Id] = o;
        // 完全約定した resting / 板に乗らなかった incoming は postMatchBook に含まれないため、
        // playerOrder（元数量）を明示的に追加。
        dict[playerOrder.Id] = playerOrder;
        return dict;
    }

    private Game AdvanceTurn(Game game, OrderBook book, int nextOrderId,
        IReadOnlyDictionary<string, Portfolio> allPortfolios, int fee)
    {
        var newPrices = Fluctuator.Fluctuate(game.Prices);
        var (expiredBook, expired) = book.ExpireOrders(game.Turn + 1);

        // 失効注文の予約解放
        var afterRelease = SettlementProcessor.ReleaseExpired(expired, allPortfolios, fee);

        var (player, computers) = SplitPortfolios(game.Player, afterRelease);
        return new Game(player, game.Turn + 1, expiredBook, nextOrderId, game.Instruments, newPrices, computers);
    }
}
