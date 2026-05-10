namespace FinLearn.Core;

/// <summary>
/// コンピュータートレーダー（毎ターン自動注文を生成する仮想プレイヤー）。
/// computer1〜computer10 は各々 Portfolio を保持し、約定が発生すれば自身の Portfolio に
/// ApplyTrade される。Portfolio は <see cref="Portfolio.CreateInfinite"/> による「∞」モード
/// で初期化されるため、現金・保有数量は不変（add/sub 後も等値）。
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
    /// 注文を生成してOrderBookに追加する。
    /// computer1〜computer10 の10プレイヤーが各自 買い1件（株価の85〜105%）と
    /// 売り1件（株価の95〜115%）を銘柄ランダムで発注する（合計20件）。
    /// 買い10件をすべて処理した後に売り10件を処理することで、売り注文時点で板に乗った
    /// 他プレイヤーの買い注文と価格交差すれば約定する。
    /// 約定があればその両当事者（computer{i}）の Portfolio に ApplyTrade される。
    /// </summary>
    public (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders, IReadOnlyDictionary<string, Portfolio> UpdatedComputerPortfolios) PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn,
        IReadOnlyDictionary<string, Portfolio> computerPortfolios)
    {
        var currentId = startOrderId;
        var updatedBook = book;
        var placed = new List<Order>(GameRules.ComputerTraders.Count * 2);

        // 注文ID → Order マップ（既存板の resting orders + これから追加する新規注文）。
        // 約定時に OrderFill から TraderId/Side/Instrument を逆引きするのに用いる。
        var ordersById = new Dictionary<int, Order>();
        foreach (var o in book.Orders)
            ordersById[o.Id] = o;

        var portfolios = new Dictionary<string, Portfolio>(computerPortfolios);

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
            placed.Add(order);
            ordersById[order.Id] = order;
            updatedBook = PlaceWithMatching(updatedBook, order, ordersById, portfolios, exchange.Fee);
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
            placed.Add(order);
            ordersById[order.Id] = order;
            updatedBook = PlaceWithMatching(updatedBook, order, ordersById, portfolios, exchange.Fee);
        }

        return (updatedBook, currentId, placed, portfolios);
    }

    /// <summary>
    /// 注文をマッチングし、約定があればコンピューター当事者の Portfolio に ApplyTrade、
    /// 未約定分のみ板に追加する。
    /// </summary>
    private static OrderBook PlaceWithMatching(
        OrderBook book,
        Order order,
        IReadOnlyDictionary<int, Order> ordersById,
        IDictionary<string, Portfolio> portfolios,
        int fee)
    {
        var fillResult = book.Match(order);
        var updatedBook = fillResult.UpdatedBook;

        ApplyFillsToComputerPortfolios(fillResult.Fills, ordersById, portfolios, fee);

        var fill = fillResult.GetFill(order.Id);
        var filledQty = fill?.FilledQuantity ?? 0;
        var remainingQty = order.Quantity - filledQty;
        if (remainingQty > 0)
            updatedBook = updatedBook.Add(order.WithQuantity(remainingQty));

        return updatedBook;
    }

    /// <summary>
    /// 各 OrderFill について、対応する Order の TraderId が computer であれば
    /// その Portfolio に ApplyTrade する。プレイヤーの resting order がマッチした場合は
    /// ここでは触れない（TurnProcessor の責務外として現状維持）。
    /// </summary>
    private static void ApplyFillsToComputerPortfolios(
        IReadOnlyList<OrderFill> fills,
        IReadOnlyDictionary<int, Order> ordersById,
        IDictionary<string, Portfolio> portfolios,
        int fee)
    {
        foreach (var fill in fills)
        {
            if (fill.FilledQuantity <= 0)
                continue;
            if (!ordersById.TryGetValue(fill.OrderId, out var order))
                continue;
            if (!IsComputerTrader(order.TraderId))
                continue;
            if (!portfolios.TryGetValue(order.TraderId, out var portfolio))
                continue;

            var trade = new TradeResult(
                InstrumentId: order.Instrument.Id,
                Side: order.Side,
                FilledQuantity: fill.FilledQuantity,
                TotalAmount: fill.TotalAmount,
                Fee: fee);
            var (updated, _) = portfolio.ApplyTrade(trade);
            portfolios[order.TraderId] = updated;
        }
    }
}
