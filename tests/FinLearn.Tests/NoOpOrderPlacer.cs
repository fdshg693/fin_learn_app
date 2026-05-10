namespace FinLearn.Tests;

using FinLearn.Core;

/// <summary>
/// 注文を一切生成しないテストダブル。板を空のままにしたいテスト用。
/// </summary>
public sealed class NoOpOrderPlacer : IOrderPlacer
{
    public OrderPlacementResult PlaceOrders(
        OrderBook book, IExchange exchange,
        IReadOnlyList<Instrument> instruments, int startOrderId, int currentTurn,
        IReadOnlyDictionary<string, Portfolio> traderPortfolios)
    {
        return new OrderPlacementResult(
            UpdatedBook: book,
            NextOrderId: startOrderId,
            PlacedOrders: Array.Empty<Order>(),
            Fills: Array.Empty<OrderFill>(),
            UpdatedTraderPortfolios: traderPortfolios);
    }
}
