namespace FinLearn.Tests;

using FinLearn.Core;

public class OrderTests
{
    // --- バリデーション ---

    [Fact]
    public void 数量が0以下の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, 0, 100));
    }

    [Fact]
    public void 価格が0以下の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, 5, 0));
    }

    // --- 注文タイプ ---

    [Fact]
    public void 指値注文はOrderTypeLimitでPriceを持つ()
    {
        var order = new Order(1, "player", new Instrument(1), OrderSide.Buy, 3, 100);

        Assert.Equal(OrderType.Limit, order.Type);
        Assert.Equal(100, order.Price);
    }

    [Fact]
    public void 成行注文はOrderTypeMarketでPriceがnull()
    {
        var order = Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3);

        Assert.Equal(OrderType.Market, order.Type);
        Assert.Null(order.Price);
    }

    [Fact]
    public void 成行注文にストップ価格を設定できる()
    {
        var order = Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3, stopPrice: 120);

        Assert.Equal(OrderType.Market, order.Type);
        Assert.Null(order.Price);
        Assert.Equal(120, order.StopPrice);
    }

    [Fact]
    public void ストップ価格なしの成行注文はStopPriceがnull()
    {
        var order = Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3);

        Assert.Null(order.StopPrice);
    }

    [Fact]
    public void ストップ価格が0以下の成行注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3, stopPrice: 0));
    }

    // --- 作成ターン ---

    [Fact]
    public void 指値注文に作成ターンを設定できる()
    {
        var order = new Order(1, "player", new Instrument(1), OrderSide.Buy, 5, 100, createdAtTurn: 3);

        Assert.Equal(3, order.CreatedAtTurn);
    }

    [Fact]
    public void 指値注文の作成ターンのデフォルトは0()
    {
        var order = new Order(1, "player", new Instrument(1), OrderSide.Buy, 5, 100);

        Assert.Equal(0, order.CreatedAtTurn);
    }

    [Fact]
    public void 成行注文に作成ターンを設定できる()
    {
        var order = Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3, createdAtTurn: 5);

        Assert.Equal(5, order.CreatedAtTurn);
    }

    [Fact]
    public void 成行注文の作成ターンのデフォルトは0()
    {
        var order = Order.CreateMarket(1, "player", new Instrument(1), OrderSide.Buy, 3);

        Assert.Equal(0, order.CreatedAtTurn);
    }
}
