namespace MyApp.Tests;

using MyApp.Core;

public class GameTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static Game CreateGame(int seed = 42)
    {
        return new Game(new ComputerTrader(new Random(seed)), Instruments);
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
    public void 購入成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        // 100円で1株購入（コンピューターの売り注文価格100%）
        Assert.Equal(9900, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 売却成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (bought, _) = game.Buy(exchange, instrumentId: 1, quantity: 1);
        var (result, warning) = bought.Sell(exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        // 買い: 100円。売り: 95円（コンピューターの買い注文価格95%）
        Assert.Equal(9995, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つでターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Wait(exchange);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つとOrderBookにコンピューター注文が追加される()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, _) = game.Wait(exchange);

        var totalBuys = result.OrderBook.BuyOrders(1).Count
            + result.OrderBook.BuyOrders(2).Count
            + result.OrderBook.BuyOrders(3).Count;
        var totalSells = result.OrderBook.SellOrders(1).Count
            + result.OrderBook.SellOrders(2).Count
            + result.OrderBook.SellOrders(3).Count;
        Assert.Equal(10, totalBuys);
        Assert.Equal(10, totalSells);
    }

    [Fact]
    public void 保有なしで売ろうとすると失敗する()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Sell(exchange, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void 現金不足の購入はターンが進まない()
    {
        // 1株が10001円なので現金10000円では買えない
        var exchange = TestData.CreateExchange((1, 10001), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(1, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 数量0以下の購入はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void ゲームは不変である()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (afterBuy, _) = game.Buy(exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(1, game.Turn);
        Assert.Equal(10000, game.Player.Portfolio.Cash);
        Assert.Empty(game.OrderBook.BuyOrders(1));
        Assert.Equal(2, afterBuy.Turn);
    }

    [Fact]
    public void 複数ターンの進行が正しく追跡される()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (turn2, _) = game.Buy(exchange, instrumentId: 1, quantity: 1);
        var (turn3, _) = turn2.Wait(exchange);
        var (turn4, _) = turn3.Sell(exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(1, game.Turn);
        Assert.Equal(2, turn2.Turn);
        Assert.Equal(3, turn3.Turn);
        Assert.Equal(4, turn4.Turn);
    }

    [Fact]
    public void 購入時に手数料が差し引かれる()
    {
        var exchange = TestData.CreateExchange(fee: 50, (1, 100), (2, 200), (3, 300));
        var game = CreateGame();

        var (result, warning) = game.Buy(exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        // 100円 × 1株 + 手数料50 = 150
        Assert.Equal(9850, result.Player.Portfolio.Cash);
    }
}
