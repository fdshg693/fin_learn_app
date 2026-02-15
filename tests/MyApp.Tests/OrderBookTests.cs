namespace MyApp.Tests;

using MyApp.Core;

public class OrderBookTests
{
    // --- 基本操作 ---

    [Fact]
    public void 空の注文帳は注文を持たない()
    {
        var book = new OrderBook();

        Assert.Empty(book.SellOrders(instrumentId: 1));
        Assert.Empty(book.BuyOrders(instrumentId: 1));
    }

    [Fact]
    public void 売り注文を追加できる()
    {
        var order = new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100);

        var result = new OrderBook().Add(order);

        Assert.Single(result.SellOrders(instrumentId: 1));
        Assert.Equal(order, result.SellOrders(instrumentId: 1)[0]);
    }

    [Fact]
    public void 買い注文を追加できる()
    {
        var order = new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 95);

        var result = new OrderBook().Add(order);

        Assert.Single(result.BuyOrders(instrumentId: 1));
        Assert.Equal(order, result.BuyOrders(instrumentId: 1)[0]);
    }

    // --- 重複防止・冪等性 ---

    [Fact]
    public void 同じIDの注文は最初の注文が優先され後続は無視される()
    {
        var first = new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100);
        var second = new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 200);

        var book = new OrderBook().Add(first).Add(second);

        Assert.Single(book.SellOrders(instrumentId: 1));
        Assert.Equal(5, book.SellOrders(instrumentId: 1)[0].Quantity);  // firstの値
        Assert.Equal(100, book.SellOrders(instrumentId: 1)[0].Price);   // firstの値
    }

    [Fact]
    public void 異なるIDの注文はそれぞれ追加される()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 110));

        Assert.Equal(2, book.SellOrders(instrumentId: 1).Count);
    }

    // --- ソート順 ---

    [Fact]
    public void 売り注文は価格昇順でソートされる()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 200))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Sell, 2, 150));

        var sells = book.SellOrders(instrumentId: 1);

        Assert.Equal(100, sells[0].Price);
        Assert.Equal(150, sells[1].Price);
        Assert.Equal(200, sells[2].Price);
    }

    [Fact]
    public void 買い注文は価格降順でソートされる()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 90))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 2, 100))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Buy, 4, 95));

        var buys = book.BuyOrders(instrumentId: 1);

        Assert.Equal(100, buys[0].Price);
        Assert.Equal(95, buys[1].Price);
        Assert.Equal(90, buys[2].Price);
    }

    [Fact]
    public void 異なる銘柄の注文は混ざらない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100))
            .Add(new Order(2, "computer", new Instrument(2), OrderSide.Sell, 3, 200));

        Assert.Single(book.SellOrders(instrumentId: 1));
        Assert.Single(book.SellOrders(instrumentId: 2));
        Assert.Equal(100, book.SellOrders(instrumentId: 1)[0].Price);
        Assert.Equal(200, book.SellOrders(instrumentId: 2)[0].Price);
    }

    // --- FillBuy: 買い注文の約定（売り注文とマッチ） ---

    [Fact]
    public void 買い約定_売り注文の一部を消化して約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100));

        var result = book.FillBuy(instrumentId: 1, quantity: 3);

        Assert.Equal(3, result.FilledQuantity);
        Assert.Equal(300, result.TotalAmount); // 3 * 100

        // 残り2株の売り注文が残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(2, remainingSells[0].Quantity);
        Assert.Equal(100, remainingSells[0].Price);
    }

    [Fact]
    public void 買い約定_売り注文を完全に消化()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100));

        var result = book.FillBuy(instrumentId: 1, quantity: 3);

        Assert.Equal(3, result.FilledQuantity);
        Assert.Equal(300, result.TotalAmount);
        Assert.Empty(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 買い約定_複数の売り注文にまたがって約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 5, 110));

        var result = book.FillBuy(instrumentId: 1, quantity: 5);

        Assert.Equal(5, result.FilledQuantity);
        Assert.Equal(520, result.TotalAmount); // 3*100 + 2*110

        // 残り3株@110の売り注文が残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(3, remainingSells[0].Quantity);
        Assert.Equal(110, remainingSells[0].Price);
    }

    [Fact]
    public void 買い約定_売り注文が不足して部分約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 2, 100));

        var result = book.FillBuy(instrumentId: 1, quantity: 5);

        Assert.Equal(2, result.FilledQuantity);
        Assert.Equal(200, result.TotalAmount);
        Assert.Empty(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 買い約定_売り注文なしで約定ゼロ()
    {
        var book = new OrderBook();

        var result = book.FillBuy(instrumentId: 1, quantity: 3);

        Assert.Equal(0, result.FilledQuantity);
        Assert.Equal(0, result.TotalAmount);
    }

    [Fact]
    public void 買い約定_安い売り注文から優先的に約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 2, 200))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 100));

        var result = book.FillBuy(instrumentId: 1, quantity: 3);

        Assert.Equal(3, result.FilledQuantity);
        Assert.Equal(300, result.TotalAmount); // 3*100（安い方から消化）

        // @100は完全消化、@200は残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(2, remainingSells[0].Quantity);
        Assert.Equal(200, remainingSells[0].Price);
    }

    // --- FillSell: 売り注文の約定（買い注文とマッチ） ---

    [Fact]
    public void 売り約定_買い注文の一部を消化して約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 95));

        var result = book.FillSell(instrumentId: 1, quantity: 3);

        Assert.Equal(3, result.FilledQuantity);
        Assert.Equal(285, result.TotalAmount); // 3 * 95

        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remainingBuys);
        Assert.Equal(2, remainingBuys[0].Quantity);
    }

    [Fact]
    public void 売り約定_複数の買い注文にまたがって約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 5, 90));

        var result = book.FillSell(instrumentId: 1, quantity: 5);

        Assert.Equal(5, result.FilledQuantity);
        Assert.Equal(480, result.TotalAmount); // 3*100 + 2*90

        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remainingBuys);
        Assert.Equal(3, remainingBuys[0].Quantity);
        Assert.Equal(90, remainingBuys[0].Price);
    }

    [Fact]
    public void 売り約定_買い注文なしで約定ゼロ()
    {
        var book = new OrderBook();

        var result = book.FillSell(instrumentId: 1, quantity: 3);

        Assert.Equal(0, result.FilledQuantity);
        Assert.Equal(0, result.TotalAmount);
    }

    [Fact]
    public void 売り約定_高い買い注文から優先的に約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 2, 80))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 3, 100));

        var result = book.FillSell(instrumentId: 1, quantity: 2);

        Assert.Equal(2, result.FilledQuantity);
        Assert.Equal(200, result.TotalAmount); // 2*100（高い方から消化）

        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Equal(2, remainingBuys.Count);
        Assert.Equal(1, remainingBuys[0].Quantity);  // 3-2=1 @100
        Assert.Equal(100, remainingBuys[0].Price);
        Assert.Equal(2, remainingBuys[1].Quantity);  // 2 @80（未消化）
        Assert.Equal(80, remainingBuys[1].Price);
    }

    [Fact]
    public void 売り約定_買い注文が不足して部分約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 2, 95));

        var result = book.FillSell(instrumentId: 1, quantity: 5);

        Assert.Equal(2, result.FilledQuantity);
        Assert.Equal(190, result.TotalAmount);
        Assert.Empty(result.UpdatedBook.BuyOrders(instrumentId: 1));
    }

    // --- 不変性 ---

    [Fact]
    public void 注文帳は不変である_追加しても元は変わらない()
    {
        var book = new OrderBook();
        var order = new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100);

        var newBook = book.Add(order);

        Assert.Empty(book.SellOrders(instrumentId: 1));
        Assert.Single(newBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 注文帳は不変である_約定しても元は変わらない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100));

        var result = book.FillBuy(instrumentId: 1, quantity: 3);

        // 元の注文帳は変わらない
        Assert.Equal(5, book.SellOrders(instrumentId: 1)[0].Quantity);
        // 新しい注文帳は変わっている
        Assert.Equal(2, result.UpdatedBook.SellOrders(instrumentId: 1)[0].Quantity);
    }

    // --- 買い注文は売り注文に影響しない（逆も同様） ---

    [Fact]
    public void 買い約定は買い注文に影響しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 3, 90));

        var result = book.FillBuy(instrumentId: 1, quantity: 2);

        // 売り注文は消化される
        Assert.Equal(3, result.UpdatedBook.SellOrders(instrumentId: 1)[0].Quantity);
        // 買い注文はそのまま
        Assert.Single(result.UpdatedBook.BuyOrders(instrumentId: 1));
        Assert.Equal(3, result.UpdatedBook.BuyOrders(instrumentId: 1)[0].Quantity);
    }
}
