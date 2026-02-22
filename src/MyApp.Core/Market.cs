namespace MyApp.Core;

/// <summary>
/// 市場の既定実装 — OrderBookを使った約定処理
/// </summary>
public sealed class Market : IMarket
{
    public MatchResult Execute(OrderBook book, Order order, IExchange exchange)
    {
        var fillResult = order.Side switch
        {
            OrderSide.Buy => ExecuteBuy(book, order),
            OrderSide.Sell => ExecuteSell(book, order),
            _ => throw new ArgumentOutOfRangeException(nameof(order))
        };

        var trade = new TradeResult(
            InstrumentId: order.Instrument.Id,
            Side: order.Side,
            FilledQuantity: fillResult.FilledQuantity,
            TotalAmount: fillResult.TotalAmount,
            Fee: exchange.Fee);

        return new MatchResult(trade, fillResult.UpdatedBook);
    }

    private static FillResult ExecuteBuy(OrderBook book, Order order)
    {
        return book.FillBuy(order.Instrument.Id, order.Quantity, order.Price);
    }

    private static FillResult ExecuteSell(OrderBook book, Order order)
    {
        var buyOrders = book.BuyOrders(order.Instrument.Id);
        if (buyOrders.Count == 0)
            return new FillResult(0, 0, book);

        var bestBuyPrice = buyOrders[0].Price;
        return book.FillSell(order.Instrument.Id, order.Quantity, bestBuyPrice);
    }
}
