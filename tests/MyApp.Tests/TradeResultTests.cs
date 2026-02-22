namespace MyApp.Tests;

using MyApp.Core;

public class TradeResultTests
{
    [Fact]
    public void プロパティが正しく保持される()
    {
        var result = new TradeResult(
            InstrumentId: 1, Side: OrderSide.Buy,
            FilledQuantity: 3, TotalAmount: 300, Fee: 50);

        Assert.Equal(1, result.InstrumentId);
        Assert.Equal(OrderSide.Buy, result.Side);
        Assert.Equal(3, result.FilledQuantity);
        Assert.Equal(300, result.TotalAmount);
        Assert.Equal(50, result.Fee);
    }

    [Fact]
    public void 同じ内容のTradeResultは等しい()
    {
        var a = new TradeResult(1, OrderSide.Buy, 3, 300, 50);
        var b = new TradeResult(1, OrderSide.Buy, 3, 300, 50);

        Assert.Equal(a, b);
    }

    [Fact]
    public void 異なる内容のTradeResultは等しくない()
    {
        var a = new TradeResult(1, OrderSide.Buy, 3, 300, 50);
        var b = new TradeResult(1, OrderSide.Sell, 3, 300, 50);

        Assert.NotEqual(a, b);
    }
}
