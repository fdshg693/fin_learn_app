namespace FinLearn.Tests;

using FinLearn.Core;

/// <summary>
/// 注文を一切生成しないテストダブル。板を空のままにしたいテスト用。
/// </summary>
public sealed class NoOpOrderPlacer : IOrderPlacer
{
    public (OrderBook UpdatedBook, int NextOrderId, IReadOnlyList<Order> PlacedOrders) PlaceOrders(
        OrderBook book, IExchange exchange,
        IReadOnlyList<Instrument> instruments, int startOrderId, int currentTurn)
    {
        return (book, startOrderId, Array.Empty<Order>());
    }
}
