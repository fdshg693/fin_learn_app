namespace MyApp.Tests;

using MyApp.Core;

public class TurnProcessorTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static TurnProcessor CreateProcessor(int seed = 42)
    {
        return new TurnProcessor(new ComputerTrader(new Random(seed)));
    }

    private static Game CreateGame()
    {
        return new Game(Instruments);
    }

    [Fact]
    public void 購入成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(9900, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 売却成功でターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (bought, _) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);
        var (result, warning) = processor.Sell(bought, exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(9995, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つでターンが1進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Wait(game, exchange);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つとOrderBookにコンピューター注文が追加される()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, _) = processor.Wait(game, exchange);

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
        var processor = CreateProcessor();

        var (result, warning) = processor.Sell(game, exchange, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void 現金不足の購入はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 10001), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(1, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 数量0以下の購入はターンが進まない()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void ゲーム状態は不変である()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (afterBuy, _) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);

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
        var processor = CreateProcessor();

        var (turn2, _) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);
        var (turn3, _) = processor.Wait(turn2, exchange);
        var (turn4, _) = processor.Sell(turn3, exchange, instrumentId: 1, quantity: 1);

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
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(9850, result.Player.Portfolio.Cash);
    }

    // --- 指値注文 ---

    [Fact]
    public void 指値買い注文が約定する()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1, price: 100);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値売り注文が約定する()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (bought, _) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1);
        var (result, warning) = processor.Sell(bought, exchange, instrumentId: 1, quantity: 1, price: 90);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値買い注文が約定せず注文が板に残りターンが進む()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        // 非常に低い価格で指値買い → 板の売り注文とマッチしない
        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 2, price: 1);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        // ポートフォリオは変わらない
        Assert.Equal(10000, result.Player.Portfolio.Cash);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        // 指値注文が板に残っている
        var buyOrders = result.OrderBook.BuyOrders(1);
        Assert.Contains(buyOrders, o => o.TraderId == "player" && o.Price == 1 && o.Quantity == 2);
    }

    [Fact]
    public void 指値買い注文が部分約定し未約定分が板に残る()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        // コンピューターの売り注文数より多い数量で指値買い → 部分約定
        // ComputerTrader(seed:42) は各銘柄に売り注文を出す。大量に買えば部分約定になる
        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 100, price: 100);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        // 何かしら約定している
        Assert.True(result.Player.Portfolio.QuantityOf(instrumentId: 1) > 0);
        // 全量は約定していない
        Assert.True(result.Player.Portfolio.QuantityOf(instrumentId: 1) < 100);
        // 未約定分が板に残っている
        var buyOrders = result.OrderBook.BuyOrders(1);
        Assert.Contains(buyOrders, o => o.TraderId == "player" && o.Price == 100);
    }

    [Fact]
    public void 指値注文のpriceが0以下の場合は警告を返す()
    {
        var exchange = TestData.CreateExchange((1, 100), (2, 200), (3, 300));
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, exchange, instrumentId: 1, quantity: 1, price: 0);

        Assert.Equal(Messages.PriceMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }
}
