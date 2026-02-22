namespace MyApp.Core;

/// <summary>
/// 市場の既定実装 — OrderBookの対称的マッチングを呼び出し、TradeResultを生成する
/// </summary>
public sealed class Market : IMarket
{
    public MatchResult Execute(OrderBook book, Order order, IExchange exchange)
    {
        var fillResult = book.Match(order);
        var incomingFill = fillResult.GetFill(order.Id);

        var trade = new TradeResult(
            InstrumentId: order.Instrument.Id,
            Side: order.Side,
            FilledQuantity: incomingFill?.FilledQuantity ?? 0,
            TotalAmount: incomingFill?.TotalAmount ?? 0,
            Fee: exchange.Fee);

        return new MatchResult(trade, fillResult.UpdatedBook);
    }
}
