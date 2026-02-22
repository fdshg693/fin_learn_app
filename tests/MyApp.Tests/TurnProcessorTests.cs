namespace MyApp.Tests;

using MyApp.Core;

public class TurnProcessorTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static readonly IReadOnlyDictionary<int, int> Prices =
        new Dictionary<int, int> { { 1, 100 }, { 2, 200 }, { 3, 300 } };

    private static TurnProcessor CreateProcessor(int seed = 42, IPriceFluctuator? fluctuator = null)
    {
        return new TurnProcessor(new ComputerTrader(new Random(seed)), fluctuator ?? new NoPriceFluctuator());
    }

    private static Game CreateGame(IReadOnlyDictionary<int, int>? prices = null)
    {
        return new Game(Instruments, prices ?? Prices);
    }

    [Fact]
    public void 購入成功でターンが1進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(9900, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 売却成功でターンが1進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (bought, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);
        var (result, warning) = processor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(9995, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つでターンが1進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Wait(game, fee: 0);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つとOrderBookにコンピューター注文が追加される()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, _) = processor.Wait(game, fee: 0);

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
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void 現金不足の購入はターンが進まない()
    {
        var expensivePrices = new Dictionary<int, int> { { 1, 10001 }, { 2, 200 }, { 3, 300 } };
        var game = CreateGame(expensivePrices);
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(1, result.Turn);
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 数量0以下の購入はターンが進まない()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }

    [Fact]
    public void ゲーム状態は不変である()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (afterBuy, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(1, game.Turn);
        Assert.Equal(10000, game.Player.Portfolio.Cash);
        Assert.Empty(game.OrderBook.BuyOrders(1));
        Assert.Equal(2, afterBuy.Turn);
    }

    [Fact]
    public void 複数ターンの進行が正しく追跡される()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (turn2, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);
        var (turn3, _) = processor.Wait(turn2, fee: 0);
        var (turn4, _) = processor.Sell(turn3, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(1, game.Turn);
        Assert.Equal(2, turn2.Turn);
        Assert.Equal(3, turn3.Turn);
        Assert.Equal(4, turn4.Turn);
    }

    [Fact]
    public void 購入時に手数料が差し引かれる()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 50, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(9850, result.Player.Portfolio.Cash);
    }

    // --- 指値注文 ---

    [Fact]
    public void 指値買い注文が約定する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 100);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        Assert.Equal(1, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値売り注文が約定する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (bought, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);
        var (result, warning) = processor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1, price: 90);

        Assert.Null(warning);
        Assert.Equal(3, result.Turn);
        Assert.Equal(0, result.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値買い注文が約定せず注文が板に残りターンが進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 非常に低い価格で指値買い → 板の売り注文とマッチしない
        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 2, price: 1);

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
        var game = CreateGame();
        var processor = CreateProcessor();

        // コンピューターの売り注文数より多い数量で指値買い → 部分約定
        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 100, price: 100);

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
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 0);

        Assert.Equal(Messages.PriceMustBePositive, warning);
        Assert.Equal(1, result.Turn);
    }

    // --- 株価変動 ---

    [Fact]
    public void 待つと価格が変動する()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var (result, _) = processor.Wait(game, fee: 0);

        // 価格が変動している（NoPriceFluctuator ではないので元の価格と異なりうる）
        Assert.NotEqual(game.Prices, result.Prices);
    }

    [Fact]
    public void 購入後に価格が変動する()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.NotEqual(game.Prices, result.Prices);
    }

    [Fact]
    public void 売却後に価格が変動する()
    {
        var game = CreateGame();
        var processor = CreateProcessor(fluctuator: new NoPriceFluctuator());

        // まず買ってから
        var (bought, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        // 売却時は変動あり
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var sellProcessor = CreateProcessor(fluctuator: fluctuator);
        var (result, warning) = sellProcessor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.NotEqual(bought.Prices, result.Prices);
    }

    [Fact]
    public void 価格変動はターンごとに異なる()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var (turn2, _) = processor.Wait(game, fee: 0);
        var (turn3, _) = processor.Wait(turn2, fee: 0);

        // ターン2とターン3で価格が異なる
        Assert.NotEqual(turn2.Prices, turn3.Prices);
    }

    [Fact]
    public void アクション失敗時は価格が変動しない()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        // 保有なしで売却 → 失敗
        var (result, warning) = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.Equal(game.Prices, result.Prices);
    }
}
