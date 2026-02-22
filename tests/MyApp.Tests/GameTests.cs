namespace MyApp.Tests;

using MyApp.Core;

public class GameTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static readonly IReadOnlyDictionary<int, int> Prices =
        new Dictionary<int, int> { { 1, 100 }, { 2, 200 }, { 3, 300 } };

    private static Game CreateGame()
    {
        return new Game(Instruments, Prices);
    }

    [Fact]
    public void 初期状態のターンは1()
    {
        var game = CreateGame();

        Assert.Equal(1, game.Turn);
    }

    [Fact]
    public void 初期状態のプレイヤーはデフォルトプレイヤー()
    {
        var game = CreateGame();

        Assert.Equal(10000, game.Player.Portfolio.Cash);
        Assert.Equal(0, game.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 初期状態のOrderBookは空()
    {
        var game = CreateGame();

        Assert.Empty(game.OrderBook.BuyOrders(1));
        Assert.Empty(game.OrderBook.SellOrders(1));
    }

    [Fact]
    public void 初期状態の価格が保持される()
    {
        var game = CreateGame();

        Assert.Equal(100, game.Prices[1]);
        Assert.Equal(200, game.Prices[2]);
        Assert.Equal(300, game.Prices[3]);
    }
}
