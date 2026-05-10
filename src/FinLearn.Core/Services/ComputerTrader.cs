namespace FinLearn.Core;

/// <summary>
/// コンピュータートレーダー（毎ターン自動注文を生成する仮想プレイヤー）。
/// computer1〜computer10 は各々 Portfolio を保持し、約定が発生すれば自身の Portfolio に
/// settlement される。Portfolio は <see cref="Portfolio.CreateInfinite"/> による「∞」モード
/// で初期化されるため、現金・保有数量は不変（add/sub 後も等値）。
///
/// 注文発注時に <see cref="Portfolio.ReserveBuy"/> / <see cref="Portfolio.ReserveSell"/> を
/// 呼ぶ（Infinite なので no-op）。約定の Portfolio 反映は <see cref="SettlementProcessor"/> に委譲し、
/// player の resting 注文と約定したケースもここで統一的に反映される。
/// </summary>
public sealed class ComputerTrader : IOrderPlacer
{
    public const string TraderIdPrefix = "computer";

    public static bool IsComputerTrader(string traderId) =>
        traderId.StartsWith(TraderIdPrefix, StringComparison.Ordinal);

    private readonly Random _random;

    public ComputerTrader(Random random)
    {
        _random = random;
    }

    /// <summary>
    /// computer1〜computer10 が買い1件（株価の85〜105%）と売り1件（株価の95〜115%）を発注（合計20件）。
    /// 板に乗った瞬間に対面注文（既存 resting + 同ターン computer 注文）と約定する可能性がある。
    /// 全約定を蓄積し、最後に <see cref="SettlementProcessor.SettleFills"/> で traderPortfolios へ統一適用する。
    /// </summary>
    public OrderPlacementResult PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn,
        IReadOnlyDictionary<string, Portfolio> traderPortfolios)
    {
        var currentId = startOrderId;
        var updatedBook = book;
        var placed = new List<Order>(GameRules.ComputerTraders.Count * 2);
        var allFills = new List<OrderFill>();

        // 注文ID → Order マップ。settlement 時に fill から TraderId/Side/Instrument を逆引き。
        // 既存 resting + これから placed する全注文を含む。
        var ordersById = new Dictionary<int, Order>();
        foreach (var o in book.Orders)
            ordersById[o.Id] = o;

        var portfolios = new Dictionary<string, Portfolio>(traderPortfolios);

        // 買い注文: 各 computer{i} が1件、株価の85〜105%
        for (int i = 1; i <= GameRules.ComputerTraders.Count; i++)
        {
            var traderId = $"{TraderIdPrefix}{i}";
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(GameRules.ComputerTraders.BuyPriceMinPercent, GameRules.ComputerTraders.BuyPriceMaxPercentExclusive);
            var price = Math.Max(GameRules.PriceFluctuation.MinPrice, marketPrice * percent / 100);
            var order = new Order(currentId++, traderId, instrument, OrderSide.Buy, 1, price, currentTurn, currentTurn + GameRules.DefaultOrderTtl);

            ReserveAndPlace(order, ref updatedBook, ordersById, portfolios, exchange.Fee, allFills);
            placed.Add(order);
        }

        // 売り注文: 各 computer{i} が1件、株価の95〜115%
        for (int i = 1; i <= GameRules.ComputerTraders.Count; i++)
        {
            var traderId = $"{TraderIdPrefix}{i}";
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(GameRules.ComputerTraders.SellPriceMinPercent, GameRules.ComputerTraders.SellPriceMaxPercentExclusive);
            var price = Math.Max(GameRules.PriceFluctuation.MinPrice, marketPrice * percent / 100);
            var order = new Order(currentId++, traderId, instrument, OrderSide.Sell, 1, price, currentTurn, currentTurn + GameRules.DefaultOrderTtl);

            ReserveAndPlace(order, ref updatedBook, ordersById, portfolios, exchange.Fee, allFills);
            placed.Add(order);
        }

        return new OrderPlacementResult(
            UpdatedBook: updatedBook,
            NextOrderId: currentId,
            PlacedOrders: placed,
            Fills: allFills,
            UpdatedTraderPortfolios: portfolios);
    }

    /// <summary>
    /// 1件の computer 注文を発注:
    /// 1. 当該 trader の Portfolio に予約（Infinite なので no-op）
    /// 2. 板に対しマッチング → 約定があれば fills に追加し、SettlementProcessor で全 trader に反映
    /// 3. 未約定分のみ板に追加
    /// </summary>
    private static void ReserveAndPlace(
        Order order,
        ref OrderBook book,
        Dictionary<int, Order> ordersById,
        Dictionary<string, Portfolio> portfolios,
        int fee,
        List<OrderFill> allFills)
    {
        if (portfolios.TryGetValue(order.TraderId, out var pf))
        {
            var (reserved, _) = order.Side == OrderSide.Buy
                ? pf.ReserveBuy(order.Instrument.Id, order.Quantity, order.Price!.Value, fee)
                : pf.ReserveSell(order.Instrument.Id, order.Quantity);
            portfolios[order.TraderId] = reserved;
        }

        ordersById[order.Id] = order;

        var fillResult = book.Match(order);
        book = fillResult.UpdatedBook;

        if (fillResult.Fills.Count > 0)
        {
            allFills.AddRange(fillResult.Fills);
            var postFillRemaining = SettlementProcessor.ComputePostFillRemainingQty(fillResult.Fills, ordersById);
            var settled = SettlementProcessor.SettleFills(fillResult.Fills, ordersById, postFillRemaining, portfolios, fee);
            foreach (var (traderId, p) in settled)
                portfolios[traderId] = p;
        }

        var fill = fillResult.GetFill(order.Id);
        var filledQty = fill?.FilledQuantity ?? 0;
        var remainingQty = order.Quantity - filledQty;
        if (remainingQty > 0)
            book = book.Add(order.WithQuantity(remainingQty));
    }
}
