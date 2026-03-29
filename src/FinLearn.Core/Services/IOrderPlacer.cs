namespace FinLearn.Core;

/// <summary>
/// 注文を生成してOrderBookに追加する（注文生成戦略のインターフェース）
/// </summary>
public interface IOrderPlacer
{
    (OrderBook UpdatedBook, int NextOrderId) PlaceOrders(
        OrderBook book,
        IExchange exchange,
        IReadOnlyList<Instrument> instruments,
        int startOrderId,
        int currentTurn);
}
