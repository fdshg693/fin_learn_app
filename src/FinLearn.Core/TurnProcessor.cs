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

        // computer 注文の発注 + 約定 + settlement（player resting への約定もここで反映される）
        var placement = OrderPlacer.PlaceOrders(
            game.OrderBook, exchange, game.Instruments, game.NextOrderId, game.Turn, BuildAllPortfolios(game));
        var portfolios = new Dictionary<string, Portfolio>(placement.UpdatedTraderPortfolios);

        var expiresAtTurn = game.Turn + expiresInTurns;
        var order = game.Player.CreateOrder(placement.NextOrderId, instrument, side, quantity, price, stopPrice, game.Turn, expiresAtTurn);
        var submittedOrders = Combine(placement.PlacedOrders, order);

        // 受付（予約）→ マッチング → 反映 → 板更新 を 1 ヶ所に集約。
        // 失敗パスでも outcome は computer settlement を確定維持した状態を返すので、
        // 終端の AdvanceTurn / TurnResult 構築は成功・失敗で共通化できる。
        var outcome = ExecutePlayerOrder(
            game.Player.Name, order, fee, portfolios, placement.UpdatedBook, exchange, noMatchMessage);

        var nextGame = AdvanceTurn(game, outcome.Book, placement.NextOrderId + 1, outcome.Portfolios, fee);
        return new TurnResult(nextGame, outcome.Trade, outcome.Warning, game.Turn, submittedOrders, outcome.Fills);
    }

    /// <summary>
    /// プレイヤー注文の受付・マッチング・反映・板更新を一括処理して outcome を返す。
    /// 失敗時（予約失敗 / 成行未約定 / 成行残高不足）は board と portfolios を
    /// computer settlement 直後の状態に固定したまま warning を載せる。
    /// </summary>
    private PlayerOrderOutcome ExecutePlayerOrder(
        string playerName, Order order, int fee,
        Dictionary<string, Portfolio> portfolios, OrderBook bookAfterComputerPlacement,
        IExchange exchange, string noMatchMessage)
    {
        // 指値: 事前予約（available 残高/数量チェック兼ねる）
        if (order.Price is int limitPrice)
        {
            var (reserved, reserveWarn) = order.Side == OrderSide.Buy
                ? portfolios[playerName].ReserveBuy(order.Instrument.Id, order.Quantity, limitPrice, fee)
                : portfolios[playerName].ReserveSell(order.Instrument.Id, order.Quantity);
            if (reserveWarn is not null)
                return Failed(bookAfterComputerPlacement, portfolios, reserveWarn);
            portfolios[playerName] = reserved;
        }

        var matchResult = Market.Execute(bookAfterComputerPlacement, order, exchange);

        // 成行で約定ゼロ → Wait と同じ進行
        if (order.Price is null && matchResult.Trade.FilledQuantity == 0)
            return Failed(bookAfterComputerPlacement, portfolios, noMatchMessage);

        if (order.Price is null)
        {
            // 成行: 同期 ApplyTrade。残高不足なら matchResult.UpdatedBook を捨ててロールバック。
            var (afterTrade, applyWarn) = portfolios[playerName].ApplyTrade(matchResult.Trade);
            if (applyWarn is not null)
                return Failed(bookAfterComputerPlacement, portfolios, applyWarn);
            portfolios[playerName] = afterTrade;
        }
        else
        {
            // 指値: 予約消費 + 差額返金。事前予約が成功しているので失敗パスは無い。
            var ordersById = BuildOrdersByIdSnapshot(bookAfterComputerPlacement, order);
            var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(matchResult.Fills, ordersById);
            var settled = SettlementProcessor.SettleFills(matchResult.Fills, ordersById, postFillRemaining, portfolios, fee);
            portfolios = new Dictionary<string, Portfolio>(settled);
        }

        var updatedBook = AddRemainingLimitOrder(matchResult.UpdatedBook, order, matchResult.Trade.FilledQuantity);
        return new PlayerOrderOutcome(updatedBook, portfolios, matchResult.Trade, matchResult.Fills, null);
    }

    private static PlayerOrderOutcome Failed(OrderBook book, IReadOnlyDictionary<string, Portfolio> portfolios, string warning) =>
        new(book, portfolios, null, Array.Empty<OrderFill>(), warning);

    private readonly record struct PlayerOrderOutcome(
        OrderBook Book,
        IReadOnlyDictionary<string, Portfolio> Portfolios,
        TradeResult? Trade,
        IReadOnlyList<OrderFill> Fills,
        string? Warning);

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
