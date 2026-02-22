namespace MyApp.Tests;

using MyApp.Core;

public class MarketTests
{
    [Fact]
    public void 買い約定で手数料がTradeResultに含まれる()
    {
        var market = new Market();
        var exchange = TestData.CreateExchange(fee: 50, (1, 100));
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100));
        var order = new Order(10, "player", new Instrument(1), OrderSide.Buy, 2, 100);

        var result = market.Execute(book, order, exchange);

        Assert.Equal(50, result.Trade.Fee);
        Assert.Equal(200, result.Trade.TotalAmount);
    }

    [Fact]
    public void 売り注文を実行して約定結果を返す()
    {
        var market = new Market();
        var exchange = TestData.CreateExchange((1, 100));
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 95));
        var order = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Sell, 3);

        var result = market.Execute(book, order, exchange);

        Assert.Equal(1, result.Trade.InstrumentId);
        Assert.Equal(OrderSide.Sell, result.Trade.Side);
        Assert.Equal(3, result.Trade.FilledQuantity);
        Assert.Equal(285, result.Trade.TotalAmount); // 3 * 95
    }

    [Fact]
    public void 売り約定後のOrderBookが返される()
    {
        var market = new Market();
        var exchange = TestData.CreateExchange((1, 100));
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 95));
        var order = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Sell, 3);

        var result = market.Execute(book, order, exchange);

        var remaining = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remaining);
        Assert.Equal(2, remaining[0].Quantity);
    }
}
