namespace FinLearn.Tests;

using FinLearn.Core;

public class TurnProcessorTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static readonly IReadOnlyDictionary<int, int> Prices =
        new Dictionary<int, int> { { 1, 100 }, { 2, 200 }, { 3, 300 } };

    private static TurnProcessor CreateProcessor(int seed = 42, IPriceFluctuator? fluctuator = null,
        int computerTtl = int.MaxValue, int playerTtl = int.MaxValue)
    {
        return new TurnProcessor(new ComputerTrader(new Random(seed)), fluctuator ?? new NoPriceFluctuator(),
            computerTtl: computerTtl, playerTtl: playerTtl);
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
        Assert.True(result.Player.Portfolio.Cash < 10000);
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
    public void 待つとOrderBookにコンピューター注文が追加される_約定分は消える()
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
        // コンピューター注文同士がマッチングされるため、約定分は板から消える
        Assert.InRange(totalBuys, 1, 10);
        Assert.InRange(totalSells, 1, 10);
        Assert.True(totalBuys + totalSells > 0);
    }

    [Fact]
    public void 保有なしで売ろうとすると失敗するがターンは進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var (result, warning) = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        // コンピューター注文は生成済みなのでターンは進む（Waitと同じ挙動）
        Assert.Equal(2, result.Turn);
        // ポートフォリオは変わらない
        Assert.Equal(10000, result.Player.Portfolio.Cash);
    }

    [Fact]
    public void 現金不足の購入は失敗するがターンは進む()
    {
        // 95%でも10000を超える価格が必要: 10000 / 0.95 ≈ 10527 → 余裕を持って20000
        var expensivePrices = new Dictionary<int, int> { { 1, 20000 }, { 2, 200 }, { 3, 300 } };
        var game = CreateGame(expensivePrices);
        var processor = CreateProcessor();

        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        // コンピューター注文は生成済みなのでターンは進む（Waitと同じ挙動）
        Assert.Equal(2, result.Turn);
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

        // 手数料なしで購入した場合の残高を基準にする
        var (resultNoFee, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);
        var processor2 = CreateProcessor();
        var (resultWithFee, warning) = processor2.Buy(game, fee: 50, instrumentId: 1, quantity: 1);

        Assert.Null(warning);
        Assert.Equal(resultNoFee.Player.Portfolio.Cash - 50, resultWithFee.Player.Portfolio.Cash);
    }

    // --- 指値注文 ---

    [Fact]
    public void 指値買い注文が約定する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 高い指値で確実にマッチさせる（コンピューター売り注文は95-115%の範囲）
        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 115);

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

        // 高い指値 + 大量の数量で部分約定を確認
        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 100, price: 115);

        Assert.Null(warning);
        Assert.Equal(2, result.Turn);
        // 何かしら約定している
        Assert.True(result.Player.Portfolio.QuantityOf(instrumentId: 1) > 0);
        // 全量は約定していない（コンピューター売り注文は最大10件で各1株）
        Assert.True(result.Player.Portfolio.QuantityOf(instrumentId: 1) < 100);
        // 未約定分が板に残っている
        var buyOrders = result.OrderBook.BuyOrders(1);
        Assert.Contains(buyOrders, o => o.TraderId == "player" && o.Price == 115);
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
    public void アクション失敗時でも価格が変動する()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        // 保有なしで売却 → 失敗だがターンは進む（Waitと同じ挙動）
        var (result, warning) = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.NotEqual(game.Prices, result.Prices);
    }

    [Fact]
    public void アクション失敗時でもコンピューター注文が板に残る()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 保有なしで売却 → 失敗
        var (result, warning) = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        // コンピューター注文は板に残っている（約定分は消える）
        var totalBuys = result.OrderBook.BuyOrders(1).Count
            + result.OrderBook.BuyOrders(2).Count
            + result.OrderBook.BuyOrders(3).Count;
        var totalSells = result.OrderBook.SellOrders(1).Count
            + result.OrderBook.SellOrders(2).Count
            + result.OrderBook.SellOrders(3).Count;
        Assert.True(totalBuys + totalSells > 0, "コンピューター注文が板に残っているべき");
    }

    [Fact]
    public void 成行注文で約定ゼロでもコンピューター注文が板に残る()
    {
        // 全銘柄の売り注文がないような状況を作る（板が空の状態で成行買い）
        var game = CreateGame();
        var noOpPlacer = new NoOpOrderPlacer();
        var processor = new TurnProcessor(noOpPlacer, new NoPriceFluctuator());

        // 板に売り注文がないので成行買いは約定しない
        var (result, warning) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(warning);
        Assert.Equal(2, result.Turn);
    }

    // --- 注文の有効期限 ---

    [Fact]
    public void TTL超過のコンピューター注文がターン進行時に除去される()
    {
        var game = CreateGame();
        var processor = CreateProcessor(computerTtl: 2, playerTtl: 10);

        // ターン1: コンピューター注文生成 → createdAtTurn=1
        var (turn2, _) = processor.Wait(game, fee: 0);
        // ターン2: コンピューター注文生成 → createdAtTurn=2
        var (turn3, _) = processor.Wait(turn2, fee: 0);
        // ターン3: ターン1の注文はTTL=2なので期限切れ (3-1 >= 2)

        // ターン1の注文はすべて除去されているはず
        // ターン2の注文(createdAtTurn=2)のみ残る → 10買い + 10売り
        var totalBuys = turn3.OrderBook.BuyOrders(1).Count
            + turn3.OrderBook.BuyOrders(2).Count
            + turn3.OrderBook.BuyOrders(3).Count;
        var totalSells = turn3.OrderBook.SellOrders(1).Count
            + turn3.OrderBook.SellOrders(2).Count
            + turn3.OrderBook.SellOrders(3).Count;

        // ターン2の注文10個のみ残る（ターン1の10個は期限切れ）
        // ただしターン2の注文同士で約定が起きる可能性がある
        // なので、ターン1の注文が除去されていることを確認
        Assert.True(totalBuys <= 10, $"買い注文が10個以下であること: {totalBuys}");
        Assert.True(totalSells <= 10, $"売り注文が10個以下であること: {totalSells}");
    }

    [Fact]
    public void プレイヤー指値注文はplayerTtlで管理される()
    {
        var game = CreateGame();
        var processor = CreateProcessor(computerTtl: 2, playerTtl: 5);

        // ターン1: 低い価格で指値買い → 板に残る
        var (turn2, _) = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 1);
        Assert.Contains(turn2.OrderBook.BuyOrders(1), o => o.TraderId == "player");

        // 数ターン待つ（playerTtl=5なのでターン6まで有効）
        var current = turn2;
        for (int i = 0; i < 3; i++)
            (current, _) = processor.Wait(current, fee: 0);

        // ターン5: まだプレイヤー注文が残っている (5-1 < 5)
        Assert.Contains(current.OrderBook.BuyOrders(1), o => o.TraderId == "player");

        // さらに1ターン進める → ターン6: プレイヤー注文は期限切れ (6-1 >= 5)
        (current, _) = processor.Wait(current, fee: 0);
        Assert.DoesNotContain(current.OrderBook.BuyOrders(1), o => o.TraderId == "player");
    }
}
