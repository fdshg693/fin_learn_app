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
    public (OrderBook UpdatedBook, int NextOrderId) PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn)
    {
        var currentId = startOrderId;
        var updatedBook = book;

        // 買い注文: 株価の85〜105%
        for (int i = 0; i < BuyOrderCount; i++)
        {
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(85, 106); // 85〜105
            var price = Math.Max(1, marketPrice * percent / 100);
            updatedBook = updatedBook.Add(
                new Order(currentId++, TraderId, instrument, OrderSide.Buy, 1, price, currentTurn));
        }

        // 売り注文: 株価の95〜115%
        for (int i = 0; i < SellOrderCount; i++)
        {
            var instrument = instruments[_random.Next(instruments.Count)];
            if (!exchange.TryGetPrice(instrument.Id, out var marketPrice))
                continue;
            var percent = _random.Next(95, 116); // 95〜115
            var price = Math.Max(1, marketPrice * percent / 100);
            updatedBook = updatedBook.Add(
                new Order(currentId++, TraderId, instrument, OrderSide.Sell, 1, price, currentTurn));
        }

        return (updatedBook, currentId);
    }
}
