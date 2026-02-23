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
}
