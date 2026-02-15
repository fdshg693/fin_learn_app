namespace MyApp.Tests;

using MyApp.Core;

public class OrderTests
{
    [Fact]
    public void 注文はIDと注文者と銘柄と売買区分と数量と価格を持つ()
    {
        var order = new Order(
            Id: 1,
            TraderId: "player",
            Instrument: new Instrument(1),
            Side: OrderSide.Sell,
            Quantity: 5,
            Price: 100);

        Assert.Equal(1, order.Id);
        Assert.Equal("player", order.TraderId);
        Assert.Equal(new Instrument(1), order.Instrument);
        Assert.Equal(OrderSide.Sell, order.Side);
        Assert.Equal(5, order.Quantity);
        Assert.Equal(100, order.Price);
    }

    [Fact]
    public void 同じ内容の注文は等しい()
    {
        var order1 = new Order(1, "player", new Instrument(1), OrderSide.Buy, 3, 200);
        var order2 = new Order(1, "player", new Instrument(1), OrderSide.Buy, 3, 200);

        Assert.Equal(order1, order2);
    }

    [Fact]
    public void 異なる内容の注文は等しくない()
    {
        var order1 = new Order(1, "player", new Instrument(1), OrderSide.Buy, 3, 200);
        var order2 = new Order(2, "computer", new Instrument(1), OrderSide.Sell, 3, 200);

        Assert.NotEqual(order1, order2);
    }

    // --- バリデーション ---

    [Fact]
    public void 数量が0以下の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, 0, 100));
    }

    [Fact]
    public void 数量が負の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, -1, 100));
    }

    [Fact]
    public void 価格が0以下の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, 5, 0));
    }

    [Fact]
    public void 価格が負の注文は作成できない()
    {
        Assert.Throws<ArgumentException>(() =>
            new Order(1, "player", new Instrument(1), OrderSide.Buy, 5, -100));
    }
}
