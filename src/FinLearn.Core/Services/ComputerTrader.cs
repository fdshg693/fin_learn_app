namespace FinLearn.Core;

/// <summary>
/// コンピュータートレーダー（毎ターン自動注文を生成する）
/// </summary>
public sealed class ComputerTrader : IOrderPlacer
{
    private const string TraderId = "computer";
    private const int BuyOrderCount = 10;
    private const int SellOrderCount = 10;

    private readonly Random _random;

    public ComputerTrader(Random random)
    {
        _random = random;
    }

    /// <summary>
    /// 注文を生成してOrderBookに追加する。
    /// 買い注文10個（株価の85〜105%）と売り注文10個（株価の95〜115%）を銘柄にランダム分散。
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
        var placed = new List<Order>(BuyOrderCount + SellOrderCount);

        // 買い注文: 株価の85〜105%
        for (int i = 0; i < BuyOrderCount; i++)
        {
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(85, 106); // 85〜105
            var price = Math.Max(1, marketPrice * percent / 100);
            var order = new Order(currentId++, TraderId, instrument, OrderSide.Buy, 1, price, currentTurn);
            placed.Add(order);
            updatedBook = PlaceWithMatching(updatedBook, order);
        }

        // 売り注文: 株価の95〜115%
        for (int i = 0; i < SellOrderCount; i++)
        {
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(95, 116); // 95〜115
            var price = Math.Max(1, marketPrice * percent / 100);
            var order = new Order(currentId++, TraderId, instrument, OrderSide.Sell, 1, price, currentTurn);
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
