namespace MyApp.Tests;

using MyApp.Core;

public class PlayerTests
{
    [Fact]
    public void 生成時に現金10000円と空のポジションを持つ()
    {
        var player = new Player();

        Assert.Equal(10000, player.Portfolio.Cash);
        Assert.Equal(0, player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void ポジションを購入できる()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var player = new Player();

        var (result, warning) = player.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(9970, result.Portfolio.Cash);
        Assert.Equal(3, result.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(10000, player.Portfolio.Cash);
        Assert.Equal(0, player.Portfolio.QuantityOf(instrumentId: 1));
    }
}
