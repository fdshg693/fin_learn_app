namespace FinLearn.Core;

/// <summary>
/// コンピュータートレーダー（毎ターン自動注文を生成する）
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
    /// </summary>
    public (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders) PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn)
    {
        var currentId = startOrderId;
        var updatedBook = book;
        var placed = new List<Order>(GameRules.ComputerTraders.Count * 2);

        // 買い注文: 各 computer{i} が1件、株価の85〜105%
        for (int i = 1; i <= GameRules.ComputerTraders.Count; i++)
        {
            var traderId = $"{TraderIdPrefix}{i}";
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(GameRules.ComputerTraders.BuyPriceMinPercent, GameRules.ComputerTraders.BuyPriceMaxPercentExclusive);
            var price = Math.Max(GameRules.PriceFluctuation.MinPrice, marketPrice * percent / 100);
            var order = new Order(currentId++, traderId, instrument, OrderSide.Buy, 1, price, currentTurn);
            placed.Add(order);
            updatedBook = PlaceWithMatching(updatedBook, order);
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
            var order = new Order(currentId++, traderId, instrument, OrderSide.Sell, 1, price, currentTurn);
            placed.Add(order);
            updatedBook = PlaceWithMatching(updatedBook, order);
        }

        return (updatedBook, currentId, placed);
    }

    /// <summary>
    /// 注文をマッチングし、未約定分のみ板に追加する。
    /// </summary>
    private static OrderBook PlaceWithMatching(OrderBook book, Order order)
    {
        var fillResult = book.Match(order);
        var updatedBook = fillResult.UpdatedBook;

        var fill = fillResult.GetFill(order.Id);
        var filledQty = fill?.FilledQuantity ?? 0;
        var remainingQty = order.Quantity - filledQty;
        if (remainingQty > 0)
            updatedBook = updatedBook.Add(order.WithQuantity(remainingQty));

        return updatedBook;
    }
}
