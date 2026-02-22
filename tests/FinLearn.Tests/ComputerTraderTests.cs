namespace FinLearn.Tests;

using FinLearn.Core;

public class ComputerTraderTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    [Fact]
    public void 買い注文が合計10個生成される()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        var totalBuys = book.BuyOrders(1).Count + book.BuyOrders(2).Count + book.BuyOrders(3).Count;
        Assert.Equal(10, totalBuys);
    }

    [Fact]
    public void 売り注文が合計10個生成される()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        var totalSells = book.SellOrders(1).Count + book.SellOrders(2).Count + book.SellOrders(3).Count;
        Assert.Equal(10, totalSells);
    }

    [Fact]
    public void 買い注文の価格は株価の95パーセント()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        foreach (var order in book.BuyOrders(1))
            Assert.Equal(95, order.Price);
        foreach (var order in book.BuyOrders(2))
            Assert.Equal(190, order.Price);
        foreach (var order in book.BuyOrders(3))
            Assert.Equal(285, order.Price);
    }

    [Fact]
    public void 売り注文の価格は株価の100パーセント()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        foreach (var order in book.SellOrders(1))
            Assert.Equal(100, order.Price);
        foreach (var order in book.SellOrders(2))
            Assert.Equal(200, order.Price);
        foreach (var order in book.SellOrders(3))
            Assert.Equal(300, order.Price);
    }

    [Fact]
    public void 全注文の数量は1()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        for (int id = 1; id <= 3; id++)
        {
            foreach (var order in book.BuyOrders(id))
                Assert.Equal(1, order.Quantity);
            foreach (var order in book.SellOrders(id))
                Assert.Equal(1, order.Quantity);
        }
    }

    [Fact]
    public void 全注文のTraderIdはcomputer()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        for (int id = 1; id <= 3; id++)
        {
            foreach (var order in book.BuyOrders(id))
                Assert.Equal("computer", order.TraderId);
            foreach (var order in book.SellOrders(id))
                Assert.Equal("computer", order.TraderId);
        }
    }

    [Fact]
    public void NextOrderIdはstartIdプラス20()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (_, nextId) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 100);

        Assert.Equal(120, nextId);
    }

    [Fact]
    public void 元のOrderBookは変更されない()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var original = new OrderBook();

        trader.PlaceOrders(original, exchange, Instruments, startOrderId: 1);

        Assert.Empty(original.BuyOrders(1));
        Assert.Empty(original.SellOrders(1));
    }

    [Fact]
    public void 株価が1の場合に買い注文の価格は最低1()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 1), (2, 1), (3, 1));

        var (book, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1);

        for (int id = 1; id <= 3; id++)
        {
            foreach (var order in book.BuyOrders(id))
                Assert.Equal(1, order.Price);
        }
    }
}
