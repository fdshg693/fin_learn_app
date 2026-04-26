namespace FinLearn.Tests;

using FinLearn.Core;

public class ComputerTraderTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    [Fact]
    public void 買い注文が最大10個生成される_約定分は板から消える()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        var totalBuys = book.BuyOrders(1).Count + book.BuyOrders(2).Count + book.BuyOrders(3).Count;
        Assert.InRange(totalBuys, 1, 10);
    }

    [Fact]
    public void 売り注文が最大10個生成される_約定分は板から消える()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        var totalSells = book.SellOrders(1).Count + book.SellOrders(2).Count + book.SellOrders(3).Count;
        Assert.InRange(totalSells, 1, 10);
    }

    [Fact]
    public void 買い注文の価格は株価の85から105パーセントの範囲()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        foreach (var order in book.BuyOrders(1))
            Assert.InRange(order.Price!.Value, 85, 105);
        foreach (var order in book.BuyOrders(2))
            Assert.InRange(order.Price!.Value, 170, 210);
        foreach (var order in book.BuyOrders(3))
            Assert.InRange(order.Price!.Value, 255, 315);
    }

    [Fact]
    public void 売り注文の価格は株価の95から115パーセントの範囲()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        foreach (var order in book.SellOrders(1))
            Assert.InRange(order.Price!.Value, 95, 115);
        foreach (var order in book.SellOrders(2))
            Assert.InRange(order.Price!.Value, 190, 230);
        foreach (var order in book.SellOrders(3))
            Assert.InRange(order.Price!.Value, 285, 345);
    }

    [Fact]
    public void 注文にCreatedAtTurnが設定される()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 5);

        for (int id = 1; id <= 3; id++)
        {
            foreach (var order in book.BuyOrders(id))
                Assert.Equal(5, order.CreatedAtTurn);
            foreach (var order in book.SellOrders(id))
                Assert.Equal(5, order.CreatedAtTurn);
        }
    }

    [Fact]
    public void 全注文の数量は1()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

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

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

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

        var (_, nextId, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 100, currentTurn: 1);

        Assert.Equal(120, nextId);
    }

    [Fact]
    public void 元のOrderBookは変更されない()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var original = new OrderBook();

        trader.PlaceOrders(original, exchange, Instruments, startOrderId: 1, currentTurn: 1);

        Assert.Empty(original.BuyOrders(1));
        Assert.Empty(original.SellOrders(1));
    }

    [Fact]
    public void 株価が1の場合に買い注文の価格は最低1()
    {
        var trader = new ComputerTrader(new Random(42));
        var exchange = TestData.CreateExchange((1, 1), (2, 1), (3, 1));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        for (int id = 1; id <= 3; id++)
        {
            foreach (var order in book.BuyOrders(id))
                Assert.Equal(1, order.Price);
        }
    }

    [Fact]
    public void コンピューター注文は既存の板とマッチングされる()
    {
        // 板に売り注文（価格100）が1件ある
        var existingSell = new Order(1, "other", new Instrument(1), OrderSide.Sell, 1, 100, 0);
        var book = new OrderBook().Add(existingSell);
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));

        // seedを調整: 買い注文が銘柄1に対して100以上の価格で生成されることを確認するため
        // 複数の買い注文を生成するので、少なくとも1つは銘柄1に対して価格>=100になるはず
        var trader = new ComputerTrader(new Random(42));
        var (resultBook, _, _) = trader.PlaceOrders(book, exchange, Instruments, startOrderId: 2, currentTurn: 1);

        // 既存の売り注文（価格100）がマッチングされて板から消えているか、
        // または部分的に約定して数量が減っている
        var remainingSells = resultBook.SellOrders(1);
        var originalSellRemaining = remainingSells.Any(o => o.Id == 1);

        // 銘柄1に対する買い注文が100以上の価格で生成されていれば、既存の売り注文と約定するはず
        var buysForInst1 = resultBook.BuyOrders(1);
        // マッチングが起きていれば：既存売り注文が消えているか、買い注文の一部が消えている
        // 少なくとも全20件がそのまま板に残ることはない（交差する注文があるため）
        var totalOrders = 0;
        for (int id = 1; id <= 3; id++)
        {
            totalOrders += resultBook.BuyOrders(id).Count + resultBook.SellOrders(id).Count;
        }
        // マッチングなしなら21件（既存1+新規20）、マッチングありなら21未満
        Assert.True(totalOrders < 21, "コンピューター注文が既存板とマッチングされるべき");
    }

    [Fact]
    public void コンピューター注文同士が同一ターン内でマッチングされる()
    {
        // 空の板でコンピューター注文を生成
        // 買い@105%と売り@95%のような交差する注文が生成されれば約定するはず
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var trader = new ComputerTrader(new Random(42));

        var (book, _, _) = trader.PlaceOrders(new OrderBook(), exchange, Instruments, startOrderId: 1, currentTurn: 1);

        // マッチングなしなら20件、マッチングありなら20未満
        var totalOrders = 0;
        for (int id = 1; id <= 3; id++)
        {
            totalOrders += book.BuyOrders(id).Count + book.SellOrders(id).Count;
        }
        // 買い(85-105%)と売り(95-115%)で範囲が重なるため、同一銘柄で交差する注文が存在しうる
        Assert.True(totalOrders < 20, "コンピューター注文同士が同一ターン内でマッチングされるべき");
    }

    [Fact]
    public void コンピューター注文の未約定分のみ板に残る()
    {
        // 板に売り注文（価格90、数量1）がある状態で、コンピューター買い注文がマッチングされたら
        // 約定した分は板から消え、未約定分のみ残る
        var existingSell = new Order(1, "other", new Instrument(1), OrderSide.Sell, 1, 90, 0);
        var book = new OrderBook().Add(existingSell);
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var trader = new ComputerTrader(new Random(42));

        var (resultBook, _, _) = trader.PlaceOrders(book, exchange, Instruments, startOrderId: 2, currentTurn: 1);

        // 売り@90はどの買い注文（85-105）とも交差しやすいので約定するはず
        var remainingSell = resultBook.SellOrders(1).FirstOrDefault(o => o.Id == 1);
        Assert.Null(remainingSell); // 数量1なので完全約定して消えるはず
    }

    [Fact]
    public void PlaceOrders_は実際に板に追加した注文をPlacedOrdersで返す()
    {
        var trader = new ComputerTrader(new Random(42));
        var instruments = new[] { TestData.Instrument1, TestData.Instrument2 };
        var exchange = TestData.CreateExchange(fee: 0, (1, 100), (2, 100));

        var (book, nextId, placed) = trader.PlaceOrders(
            new OrderBook(), exchange, instruments, startOrderId: 100, currentTurn: 5);

        Assert.Equal(20, placed.Count);
        Assert.All(placed, o => Assert.Equal("computer", o.TraderId));
        Assert.All(placed, o => Assert.Equal(5, o.CreatedAtTurn));
        Assert.Equal(100, placed[0].Id);
        Assert.Equal(119, placed[^1].Id);
        Assert.Equal(120, nextId);
    }
}
