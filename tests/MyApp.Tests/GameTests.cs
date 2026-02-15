namespace MyApp.Tests;

using MyApp.Core;

public class GameTests
{
    [Fact]
    public void 初期状態のターンは1()
    {
        var game = new Game();

        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void 初期状態のプレイヤーはデフォルトプレイヤー()
    {
        var game = new Game();

        Assert.Equal(10000, game.Player.Portfolio.Cash);
        Assert.Equal(0, game.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 購入成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(9970, result.Player.Portfolio.Cash);
        Assert.Equal(3, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 売却成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (bought, _) = game.Buy(exchange, instrumentId: 1, quantity: 3);
        var (result, warning) = bought.Sell(exchange, instrumentId: 1, quantity: 2);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(9990, result.Player.Portfolio.Cash);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 待つでターンが1進む()
    {
        var game = new Game();

        var (result, warning) = game.Wait();

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 現金不足の購入はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 1001);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(1, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 保有数超過の売却はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (result, warning) = game.Sell(exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void 数量0以下の購入はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void ゲームは不変である()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (afterBuy, _) = game.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Equal(1, game.Turn);
        Assert.Equal(10000, game.Player.Portfolio.Cash);
        Assert.Equal(2, afterBuy.Turn);
        Assert.Equal(9970, afterBuy.Player.Portfolio.Cash);
    }

    [Fact]
    public void 複数ターンの進行が正しく追跡される()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var game = new Game();

        var (turn2, _) = game.Buy(exchange, instrumentId: 1, quantity: 3);
        var (turn3, _) = turn2.Wait();
        var (turn4, _) = turn3.Sell(exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(1, game.Turn);
        Assert.Equal(2, turn2.Turn);
        Assert.Equal(3, turn3.Turn);
        Assert.Equal(4, turn4.Turn);
    }
}
