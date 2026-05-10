namespace FinLearn.Tests;

using FinLearn.Core;

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

    // --- Match: 買い注文の約定（売り注文とマッチ） ---

    [Fact]
    public void 買い約定_売り注文の一部を消化して約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 100));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(3, incomingFill.FilledQuantity);
        Assert.Equal(300, incomingFill.TotalAmount); // 3 * 100

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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(3, incomingFill.FilledQuantity);
        Assert.Equal(300, incomingFill.TotalAmount);
        Assert.Empty(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 買い約定_複数の売り注文にまたがって約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 5, 110));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 5, 110);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(5, incomingFill.FilledQuantity);
        Assert.Equal(520, incomingFill.TotalAmount); // 3*100 + 2*110

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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 5, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(2, incomingFill.FilledQuantity);
        Assert.Equal(200, incomingFill.TotalAmount);
        Assert.Empty(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 買い約定_売り注文なしで約定ゼロ()
    {
        var book = new OrderBook();
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
    }

    [Fact]
    public void 買い約定_安い売り注文から優先的に約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 2, 200))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 100));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 200);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(3, incomingFill.FilledQuantity);
        Assert.Equal(300, incomingFill.TotalAmount); // 3*100（安い方から消化）

        // @100は完全消化、@200は残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(2, remainingSells[0].Quantity);
        Assert.Equal(200, remainingSells[0].Price);
    }

    // --- Match: 売り注文の約定（買い注文とマッチ） ---

    [Fact]
    public void 売り約定_買い注文の一部を消化して約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 95));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 3, 90);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(3, incomingFill.FilledQuantity);
        Assert.Equal(285, incomingFill.TotalAmount); // 3 * 95（約定価格は待機注文の価格）

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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 5, 85);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(5, incomingFill.FilledQuantity);
        // 約定価格は各待機注文の価格: 3*100 + 2*90 = 480
        Assert.Equal(480, incomingFill.TotalAmount);

        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remainingBuys);
        Assert.Equal(3, remainingBuys[0].Quantity);
        Assert.Equal(90, remainingBuys[0].Price);
    }

    [Fact]
    public void 売り約定_買い注文なしで約定ゼロ()
    {
        var book = new OrderBook();
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
    }

    [Fact]
    public void 売り約定_高い買い注文から優先的に約定()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 2, 80))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 3, 100));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 2, 80);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(2, incomingFill.FilledQuantity);
        // 約定価格は待機注文の価格: 2*100（高い方から消化）
        Assert.Equal(200, incomingFill.TotalAmount);

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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 5, 90);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(2, incomingFill.FilledQuantity);
        // 約定価格は待機注文の価格: 2*95 = 190
        Assert.Equal(190, incomingFill.TotalAmount);
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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);

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
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 2, 100);

        var result = book.Match(incoming);

        // 売り注文は消化される
        Assert.Equal(3, result.UpdatedBook.SellOrders(instrumentId: 1)[0].Quantity);
        // 買い注文はそのまま
        Assert.Single(result.UpdatedBook.BuyOrders(instrumentId: 1));
        Assert.Equal(3, result.UpdatedBook.BuyOrders(instrumentId: 1)[0].Quantity);
    }

    // --- 約定の価格判定 ---

    [Fact]
    public void 買い約定_買い価格以下の売り注文のみ約定する()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 90))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Sell, 3, 110));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 10, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        // 90と100は約定、110は約定しない
        Assert.NotNull(incomingFill);
        Assert.Equal(6, incomingFill.FilledQuantity);
        Assert.Equal(570, incomingFill.TotalAmount); // 3*90 + 3*100

        // 110の売り注文のみ残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(110, remainingSells[0].Price);
    }

    [Fact]
    public void 買い約定_全ての売り注文が買い価格より高い場合は約定しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 200));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
        Assert.Single(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 売り約定_売り価格以上の買い注文のみ約定する()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 110))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 3, 100))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Buy, 3, 90));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 10, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        // 110と100は約定（買い価格>=売り価格）、90は約定しない
        Assert.NotNull(incomingFill);
        Assert.Equal(6, incomingFill.FilledQuantity);
        // 約定価格は各待機注文の価格: 3*110 + 3*100 = 630
        Assert.Equal(630, incomingFill.TotalAmount);

        // 90の買い注文のみ残る
        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remainingBuys);
        Assert.Equal(90, remainingBuys[0].Price);
    }

    [Fact]
    public void 売り約定_全ての買い注文が売り価格より低い場合は約定しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 80));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
        Assert.Single(result.UpdatedBook.BuyOrders(instrumentId: 1));
    }

    [Fact]
    public void 売り約定_約定価格は待機注文の買い価格になる()
    {
        // 買い注文が110、売り注文が100 → 約定価格は待機注文（買い）の価格110
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 110));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Sell, 2, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(2, incomingFill.FilledQuantity);
        Assert.Equal(220, incomingFill.TotalAmount); // 2 * 110（待機注文の価格で約定）
    }

    // --- 各注文IDに紐づく約定結果 ---

    [Fact]
    public void 約定結果は各注文IDごとに取得できる()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 5, 110));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 5, 110);

        var result = book.Match(incoming);

        // 受注注文（買い手）の約定結果
        var buyerFill = result.GetFill(10);
        Assert.NotNull(buyerFill);
        Assert.Equal(5, buyerFill.FilledQuantity);
        Assert.Equal(520, buyerFill.TotalAmount); // 3*100 + 2*110

        // 待機注文1（売り手@100）の約定結果
        var seller1Fill = result.GetFill(1);
        Assert.NotNull(seller1Fill);
        Assert.Equal(3, seller1Fill.FilledQuantity);
        Assert.Equal(300, seller1Fill.TotalAmount); // 3*100

        // 待機注文2（売り手@110）の約定結果
        var seller2Fill = result.GetFill(2);
        Assert.NotNull(seller2Fill);
        Assert.Equal(2, seller2Fill.FilledQuantity);
        Assert.Equal(220, seller2Fill.TotalAmount); // 2*110
    }

    [Fact]
    public void 約定しなかった待機注文のFillはnullを返す()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 5, 200));
        var incoming = new Order(10, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);

        // @100は約定
        Assert.NotNull(result.GetFill(1));
        // @200は約定しない
        Assert.Null(result.GetFill(2));
    }

    // --- 成行注文のマッチング ---

    [Fact]
    public void 成行買い注文_ストップ価格なしは全売り注文とマッチする()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 2, 500));
        var incoming = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Buy, 4);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(4, incomingFill.FilledQuantity);
        // 安い方から: 3*100 + 1*500 = 800
        Assert.Equal(800, incomingFill.TotalAmount);
    }

    [Fact]
    public void 成行売り注文_ストップ価格なしは全買い注文とマッチする()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 95))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 2, 10));
        var incoming = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Sell, 4);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(4, incomingFill.FilledQuantity);
        // 高い方から: 3*95 + 1*10 = 295
        Assert.Equal(295, incomingFill.TotalAmount);
    }

    // --- 成行注文のストップ価格 ---

    [Fact]
    public void 成行買い注文_ストップ価格以下の売り注文のみ約定する()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 2, 200))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Sell, 2, 500));
        var incoming = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Buy, 10, stopPrice: 200);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        // 100と200は約定、500は約定しない
        Assert.Equal(5, incomingFill.FilledQuantity);
        Assert.Equal(700, incomingFill.TotalAmount); // 3*100 + 2*200

        // 500の売り注文のみ残る
        var remainingSells = result.UpdatedBook.SellOrders(instrumentId: 1);
        Assert.Single(remainingSells);
        Assert.Equal(500, remainingSells[0].Price);
    }

    [Fact]
    public void 成行売り注文_ストップ価格以上の買い注文のみ約定する()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 3, 100))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Buy, 2, 50))
            .Add(new Order(3, "computer", new Instrument(1), OrderSide.Buy, 2, 10));
        var incoming = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Sell, 10, stopPrice: 50);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        // 100と50は約定、10は約定しない
        Assert.Equal(5, incomingFill.FilledQuantity);
        Assert.Equal(400, incomingFill.TotalAmount); // 3*100 + 2*50

        // 10の買い注文のみ残る
        var remainingBuys = result.UpdatedBook.BuyOrders(instrumentId: 1);
        Assert.Single(remainingBuys);
        Assert.Equal(10, remainingBuys[0].Price);
    }

    [Fact]
    public void 成行買い注文_全売り注文がストップ価格超で約定ゼロ()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Sell, 5, 500));
        var incoming = Order.CreateMarket(10, "player", new Instrument(1), OrderSide.Buy, 3, stopPrice: 200);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
        Assert.Single(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    // --- 自己約定の防止 ---

    [Fact]
    public void 同一TraderIdの売り注文には買い指値で約定しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "alice", new Instrument(1), OrderSide.Sell, 5, 100));
        var incoming = new Order(10, "alice", new Instrument(1), OrderSide.Buy, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Equal(0, incomingFill.TotalAmount);
        Assert.Single(result.UpdatedBook.SellOrders(instrumentId: 1));
    }

    [Fact]
    public void 同一TraderIdの買い注文には売り指値で約定しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "alice", new Instrument(1), OrderSide.Buy, 5, 100));
        var incoming = new Order(10, "alice", new Instrument(1), OrderSide.Sell, 3, 100);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
        Assert.Single(result.UpdatedBook.BuyOrders(instrumentId: 1));
    }

    [Fact]
    public void 同一TraderIdは成行注文でも約定しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "alice", new Instrument(1), OrderSide.Sell, 5, 100));
        var incoming = Order.CreateMarket(10, "alice", new Instrument(1), OrderSide.Buy, 3);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(0, incomingFill.FilledQuantity);
    }

    [Fact]
    public void 同一TraderIdの注文をスキップして異なるTraderIdの注文と約定する()
    {
        // 板：自分(alice)の売り@100が先頭、bobの売り@110が次
        // → 自己約定はスキップされ、bobの注文と約定する
        var book = new OrderBook()
            .Add(new Order(1, "alice", new Instrument(1), OrderSide.Sell, 5, 100))
            .Add(new Order(2, "bob", new Instrument(1), OrderSide.Sell, 3, 110));
        var incoming = new Order(10, "alice", new Instrument(1), OrderSide.Buy, 3, 110);

        var result = book.Match(incoming);
        var incomingFill = result.GetFill(10);

        Assert.NotNull(incomingFill);
        Assert.Equal(3, incomingFill.FilledQuantity);
        Assert.Equal(3 * 110, incomingFill.TotalAmount);
        // 自分の売り注文は板に残り、bobの売り注文が消化される
        Assert.Contains(result.UpdatedBook.SellOrders(1), o => o.TraderId == "alice");
        Assert.DoesNotContain(result.UpdatedBook.SellOrders(1), o => o.TraderId == "bob");
    }

    // --- 注文の有効期限 ---

    [Fact]
    public void ExpiresAtTurn到達で注文が除去される()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 1, expiresAtTurn: 3))
            .Add(new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 110, createdAtTurn: 1, expiresAtTurn: 3));

        // currentTurn=3 で ExpiresAtTurn=3 の注文は除去される (3 >= 3)
        var expired = book.ExpireOrders(currentTurn: 3);

        Assert.Empty(expired.BuyOrders(1));
        Assert.Empty(expired.SellOrders(1));
    }

    [Fact]
    public void ExpiresAtTurn未到達なら注文は残る()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 3, expiresAtTurn: 6))
            .Add(new Order(2, "player", new Instrument(1), OrderSide.Sell, 3, 110, createdAtTurn: 2, expiresAtTurn: 7));

        var expired = book.ExpireOrders(currentTurn: 5);

        Assert.Single(expired.BuyOrders(1));
        Assert.Single(expired.SellOrders(1));
    }

    [Fact]
    public void 注文ごとに異なるExpiresAtTurnが個別に判定される()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 1, expiresAtTurn: 3))
            .Add(new Order(2, "player", new Instrument(1), OrderSide.Buy, 3, 100, createdAtTurn: 1, expiresAtTurn: 6));

        // currentTurn=5: 注文1 (ExpiresAtTurn=3) は期限切れ、注文2 (ExpiresAtTurn=6) は有効
        var expired = book.ExpireOrders(currentTurn: 5);

        var buyOrders = expired.BuyOrders(1);
        Assert.Single(buyOrders);
        Assert.Equal("player", buyOrders[0].TraderId);
    }

    [Fact]
    public void ExpiresAtTurn未指定の注文は期限切れにならない()
    {
        // ExpiresAtTurn のデフォルトは int.MaxValue（実質無期限）
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 1));

        var expired = book.ExpireOrders(currentTurn: 1000);

        Assert.Single(expired.BuyOrders(1));
    }

    [Fact]
    public void ExpireOrdersは元のOrderBookを変更しない()
    {
        var book = new OrderBook()
            .Add(new Order(1, "computer", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 1, expiresAtTurn: 3));

        book.ExpireOrders(currentTurn: 10);

        Assert.Single(book.BuyOrders(1));
    }
}
