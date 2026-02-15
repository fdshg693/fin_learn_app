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

    [Fact]
    public void ポジションを売却できる()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var player = new Player();

        var (bought, _) = player.Buy(exchange, instrumentId: 1, quantity: 3);
        var (result, warning) = bought.Sell(exchange, instrumentId: 1, quantity: 2);

        Assert.Null(warning);
        Assert.Equal(9990, result.Portfolio.Cash);
        Assert.Equal(1, result.Portfolio.QuantityOf(instrumentId: 1));
        // 元のプレイヤーは不変
        Assert.Equal(9970, bought.Portfolio.Cash);
        Assert.Equal(3, bought.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 取引していない場合の損益は0()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var player = new Player();

        Assert.Equal(0, player.ProfitLoss(exchange));
    }

    [Fact]
    public void 株価が上がると損益がプラスになる()
    {
        var buyExchange = TestData.CreateExchange((1, 10));
        var player = new Player();

        var (bought, _) = player.Buy(buyExchange, instrumentId: 1, quantity: 100);
        // 株価が10→15に上昇
        var currentExchange = TestData.CreateExchange((1, 15));

        // 評価額 = 現金9000 + 株100×15 = 10500、損益 = 10500 - 10000 = 500
        Assert.Equal(500, bought.ProfitLoss(currentExchange));
    }
}
