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

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
        Assert.Equal(1, turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.True(turn.Game.Player.Portfolio.Cash < 10000);
    }

    [Fact]
    public void 購入成功でTradeResultが返される()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.NotNull(turn.Trade);
        Assert.Equal(1, turn.Trade.InstrumentId);
        Assert.Equal(OrderSide.Buy, turn.Trade.Side);
        Assert.True(turn.Trade.FilledQuantity > 0);
        Assert.True(turn.Trade.TotalAmount > 0);
    }

    [Fact]
    public void 売却成功でTradeResultが返される()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var bought = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
        var turn = processor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.NotNull(turn.Trade);
        Assert.Equal(1, turn.Trade.InstrumentId);
        Assert.Equal(OrderSide.Sell, turn.Trade.Side);
        Assert.True(turn.Trade.FilledQuantity > 0);
    }

    [Fact]
    public void 待つではTradeResultがnullになる()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Wait(game, fee: 0);

        Assert.Null(turn.Warning);
        Assert.Null(turn.Trade);
    }

    [Fact]
    public void 失敗時はTradeResultがnullになる()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(turn.Warning);
        Assert.Null(turn.Trade);
    }

    [Fact]
    public void 売却成功でターンが1進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var bought = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
        var turn = processor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.Equal(3, turn.Game.Turn);
        Assert.Equal(0, turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 待つでターンが1進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Wait(game, fee: 0);

        Assert.Null(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
        Assert.Equal(10000, turn.Game.Player.Portfolio.Cash);
    }

    [Fact]
    public void 待つとOrderBookにコンピューター注文が追加される_約定分は消える()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Wait(game, fee: 0).Game;

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

        var turn = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(turn.Warning);
        // コンピューター注文は生成済みなのでターンは進む（Waitと同じ挙動）
        Assert.Equal(2, turn.Game.Turn);
        // ポートフォリオは変わらない
        Assert.Equal(10000, turn.Game.Player.Portfolio.Cash);
    }

    [Fact]
    public void 現金不足の購入は失敗するがターンは進む()
    {
        // 95%でも10000を超える価格が必要: 10000 / 0.95 ≈ 10527 → 余裕を持って20000
        var expensivePrices = new Dictionary<int, int> { { 1, 20000 }, { 2, 200 }, { 3, 300 } };
        var game = CreateGame(expensivePrices);
        var processor = CreateProcessor();

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.InsufficientCashToBuy, turn.Warning);
        // コンピューター注文は生成済みなのでターンは進む（Waitと同じ挙動）
        Assert.Equal(2, turn.Game.Turn);
        Assert.Equal(10000, turn.Game.Player.Portfolio.Cash);
    }

    [Fact]
    public void 数量0以下の購入はターンが進まない()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, turn.Warning);
        Assert.Equal(1, turn.Game.Turn);
    }

    [Fact]
    public void ゲーム状態は不変である()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var afterBuy = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;

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

        var turn2 = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
        var turn3 = processor.Wait(turn2, fee: 0).Game;
        var turn4 = processor.Sell(turn3, fee: 0, instrumentId: 1, quantity: 1).Game;

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
        var resultNoFee = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
        var processor2 = CreateProcessor();
        var turnWithFee = processor2.Buy(game, fee: 50, instrumentId: 1, quantity: 1);

        Assert.Null(turnWithFee.Warning);
        Assert.Equal(resultNoFee.Player.Portfolio.Cash - 50, turnWithFee.Game.Player.Portfolio.Cash);
    }

    // --- 指値注文 ---

    [Fact]
    public void 指値買い注文が約定する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 高い指値で確実にマッチさせる（コンピューター売り注文は95-115%の範囲）
        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 115);

        Assert.Null(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
        Assert.Equal(1, turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値売り注文が約定する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var bought = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;
        var turn = processor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1, price: 90);

        Assert.Null(turn.Warning);
        Assert.Equal(3, turn.Game.Turn);
        Assert.Equal(0, turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 指値買い注文が約定せず注文が板に残りターンが進む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 非常に低い価格で指値買い → 板の売り注文とマッチしない
        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 2, price: 1);

        Assert.Null(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
        // 予約モデル: Cash は available のみ。発注時に price*qty=2 が available → reserved に移る
        Assert.Equal(10000 - 2, turn.Game.Player.Portfolio.Cash);
        Assert.Equal(2, turn.Game.Player.Portfolio.ReservedCash);
        Assert.Equal(0, turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1));
        // 指値注文が板に残っている
        var buyOrders = turn.Game.OrderBook.BuyOrders(1);
        Assert.Contains(buyOrders, o => o.TraderId == "player" && o.Price == 1 && o.Quantity == 2);
    }

    [Fact]
    public void 指値買い注文が部分約定し未約定分が板に残る()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 予約モデルでは事前予約分の現金が必要。115×80 = 9200 で初期 10000 内に収まる量で部分約定を狙う。
        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 80, price: 115);

        Assert.Null(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
        // 何かしら約定している
        Assert.True(turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1) > 0);
        // 全量は約定していない（コンピューター売り注文は最大10件で各1株）
        Assert.True(turn.Game.Player.Portfolio.QuantityOf(instrumentId: 1) < 80);
        // 未約定分が板に残っている
        var buyOrders = turn.Game.OrderBook.BuyOrders(1);
        Assert.Contains(buyOrders, o => o.TraderId == "player" && o.Price == 115);
    }

    [Fact]
    public void 指値注文のpriceが0以下の場合は警告を返す()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 0);

        Assert.Equal(Messages.PriceMustBePositive, turn.Warning);
        Assert.Equal(1, turn.Game.Turn);
    }

    // --- 株価変動 ---

    [Fact]
    public void 待つと価格が変動する()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var result = processor.Wait(game, fee: 0).Game;

        // 価格が変動している（NoPriceFluctuator ではないので元の価格と異なりうる）
        Assert.NotEqual(game.Prices, result.Prices);
    }

    [Fact]
    public void 購入後に価格が変動する()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.NotEqual(game.Prices, turn.Game.Prices);
    }

    [Fact]
    public void 売却後に価格が変動する()
    {
        var game = CreateGame();
        var processor = CreateProcessor(fluctuator: new NoPriceFluctuator());

        // まず買ってから
        var bought = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1).Game;

        // 売却時は変動あり
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var sellProcessor = CreateProcessor(fluctuator: fluctuator);
        var turn = sellProcessor.Sell(bought, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Null(turn.Warning);
        Assert.NotEqual(bought.Prices, turn.Game.Prices);
    }

    [Fact]
    public void 価格変動はターンごとに異なる()
    {
        var game = CreateGame();
        var fluctuator = new RandomPriceFluctuator(new Random(42));
        var processor = CreateProcessor(fluctuator: fluctuator);

        var turn2 = processor.Wait(game, fee: 0).Game;
        var turn3 = processor.Wait(turn2, fee: 0).Game;

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
        var turn = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(turn.Warning);
        Assert.NotEqual(game.Prices, turn.Game.Prices);
    }

    [Fact]
    public void アクション失敗時でもコンピューター注文が板に残る()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // 保有なしで売却 → 失敗
        var turn = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(turn.Warning);
        // コンピューター注文は板に残っている（約定分は消える）
        var totalBuys = turn.Game.OrderBook.BuyOrders(1).Count
            + turn.Game.OrderBook.BuyOrders(2).Count
            + turn.Game.OrderBook.BuyOrders(3).Count;
        var totalSells = turn.Game.OrderBook.SellOrders(1).Count
            + turn.Game.OrderBook.SellOrders(2).Count
            + turn.Game.OrderBook.SellOrders(3).Count;
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
        var turn = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.NotNull(turn.Warning);
        Assert.Equal(2, turn.Game.Turn);
    }

    // --- 注文の有効期限 ---

    [Fact]
    public void コンピューター注文はデフォルトTTLで期限切れになる()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // ターン1: コンピューター注文生成 → ExpiresAtTurn = 1 + 2 = 3
        var turn2 = processor.Wait(game, fee: 0).Game;
        // ターン2: コンピューター注文生成 → ExpiresAtTurn = 2 + 2 = 4
        var turn3 = processor.Wait(turn2, fee: 0).Game;
        // ターン3への進行時に ExpireOrders(currentTurn=3) が呼ばれ、
        // ターン1の注文(ExpiresAtTurn=3)は除去される (3 >= 3)

        // ターン1由来のコンピューター注文が残っていないことを確認
        var allOrders = turn3.OrderBook.Orders;
        Assert.DoesNotContain(allOrders, o => o.CreatedAtTurn == 1);
    }

    [Fact]
    public void プレイヤー注文はデフォルト2ターンで生成ターンと次のターンに残る()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // ターン1: 約定しない低価格で指値買い → 板に残る
        var turn2 = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 1).Game;
        Assert.Contains(turn2.OrderBook.BuyOrders(1), o => o.TraderId == "player");

        // ターン2 → 3 進行時に ExpireOrders(currentTurn=3) → プレイヤー注文(ExpiresAtTurn=3)は除去
        var turn3 = processor.Wait(turn2, fee: 0).Game;
        Assert.DoesNotContain(turn3.OrderBook.BuyOrders(1), o => o.TraderId == "player");
    }

    [Fact]
    public void プレイヤーがexpiresInTurnsを指定できる()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        // ターン1: expiresInTurns=5 → ExpiresAtTurn = 1 + 5 = 6
        var turn2 = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 1, expiresInTurns: 5).Game;
        Assert.Contains(turn2.OrderBook.BuyOrders(1), o => o.TraderId == "player");

        // ターン5まで進める（4回 Wait） → ExpireOrders(currentTurn=2..5) → 全て < 6 で残る
        var current = turn2;
        for (int i = 0; i < 3; i++)
            current = processor.Wait(current, fee: 0).Game;
        Assert.Equal(5, current.Turn);
        Assert.Contains(current.OrderBook.BuyOrders(1), o => o.TraderId == "player");

        // ターン6に進める → ExpireOrders(6) → 6 >= 6 で期限切れ
        current = processor.Wait(current, fee: 0).Game;
        Assert.DoesNotContain(current.OrderBook.BuyOrders(1), o => o.TraderId == "player");
    }

    [Fact]
    public void expiresInTurns0以下でRejectedが返る()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, expiresInTurns: 0);

        Assert.NotNull(result.Warning);
        Assert.Equal(Messages.ExpiresInTurnsMustBePositive, result.Warning);
        Assert.Equal(1, result.Game.Turn); // ターン進行しない
    }

    // --- 予約モデル（指値発注時の資源予約）テスト ---

    [Fact]
    public void 指値買い発注で即座にreservedCashが増えavailableCashが減る()
    {
        var game = CreateGame();
        // 約定しない低価格で発注（板の売り注文と交差しない）
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        var result = processor.Buy(game, fee: 50, instrumentId: 1, quantity: 3, price: 100);

        Assert.Null(result.Warning);
        // 予約 = 3*100 + 50 = 350
        Assert.Equal(10000 - 350, result.Game.Player.Portfolio.Cash);
        Assert.Equal(350, result.Game.Player.Portfolio.ReservedCash);
    }

    [Fact]
    public void 指値売り発注でreservedPositionsが増えavailableQuantityが減る()
    {
        // プレイヤーは銘柄1を10株保有した状態でスタート
        var basePlayer = new Player();
        var withPosition = basePlayer.WithPortfolio(new Portfolio(cash: 10000, positions: new[] { new Position(new Instrument(1), 10) }));
        var game = new Game(withPosition, turn: 1, new OrderBook(), nextOrderId: 1, Instruments, Prices);
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        var result = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 4, price: 200);

        Assert.Null(result.Warning);
        Assert.Equal(10, result.Game.Player.Portfolio.QuantityOf(1));            // 全保有不変
        Assert.Equal(4, result.Game.Player.Portfolio.ReservedQuantityOf(1));
        Assert.Equal(6, result.Game.Player.Portfolio.AvailableQuantityOf(1));
    }

    [Fact]
    public void 指値が失効すると予約が_available_に戻る()
    {
        var game = CreateGame();
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        // ターン1で指値買い発注（expiresInTurns=2 → ExpiresAtTurn=3）
        var afterBuy = processor.Buy(game, fee: 50, instrumentId: 1, quantity: 3, price: 100, expiresInTurns: 2).Game;
        Assert.Equal(350, afterBuy.Player.Portfolio.ReservedCash);

        // ターン2で Wait（同じ fee=50 で消費的に整合）→ ExpireOrders(3) で player 注文が失効、予約が解放される
        var afterWait1 = processor.Wait(afterBuy, fee: 50).Game;
        Assert.Equal(0, afterWait1.Player.Portfolio.ReservedCash);
        Assert.Equal(10000, afterWait1.Player.Portfolio.Cash);
    }

    [Fact]
    public void 指値で予約失敗_残高不足_はターン進行_warning_Fills空_player注文も含む()
    {
        var game = CreateGame();
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        // 初期 cash 10000 を超える予約: 50000
        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 500, price: 100);

        Assert.Equal(Messages.InsufficientCashToBuy, result.Warning);
        Assert.Equal(2, result.Game.Turn);                          // ターンは進む
        Assert.Empty(result.Fills);
        Assert.Single(result.SubmittedOrders);                       // player 注文は submitted に含まれる
        Assert.Equal("player", result.SubmittedOrders[0].TraderId);
        // 予約は実行されていない
        Assert.Equal(10000, result.Game.Player.Portfolio.Cash);
        Assert.Equal(0, result.Game.Player.Portfolio.ReservedCash);
    }

    /// <summary>
    /// バグ修正検証: プレイヤーの過去ターン resting 指値が当ターンの computer 注文と約定すると
    /// player Portfolio に正しく反映される（旧コードでは反映されなかった）。
    /// </summary>
    [Fact]
    public void プレイヤーの過去ターン_resting_指値が_computer_注文と約定するとPortfolioに反映される()
    {
        // プレイヤーは銘柄1を 5 株保有
        var basePlayer = new Player();
        var withPosition = basePlayer.WithPortfolio(new Portfolio(cash: 10000, positions: new[] { new Position(new Instrument(1), 5) }));
        var game = new Game(withPosition, turn: 1, new OrderBook(), nextOrderId: 1, Instruments, Prices);

        // ターン1で売り指値を発注（プレイヤー以外と約定するように低めの価格 50）。
        // computer 買い注文の範囲は株価100×85〜105% = 85〜105 なので、50 は確実に交差して約定する。
        var processor = new TurnProcessor(new ComputerTrader(new Random(42)), new NoPriceFluctuator());
        var afterSell = processor.Sell(game, fee: 0, instrumentId: 1, quantity: 1, price: 50, expiresInTurns: 5);
        var gameAfterSell = afterSell.Game;

        // 発注時点では未約定なら板に残り、reservedPositions に1株移動 + cash は変わらない
        // （computer 注文がそのターンで約定する可能性もあるが、それも player Portfolio に反映される）

        // ターン2で Wait → computer の新しい買い注文が player の resting sell @50 と確実に約定
        var afterWait = processor.Wait(gameAfterSell, fee: 0);
        var finalGame = afterWait.Game;

        // 過去ターンの resting 売りが約定 → 全保有 5→4、reserved 1→0、cash は 10000 + 50 = 10050
        Assert.Equal(4, finalGame.Player.Portfolio.QuantityOf(1));
        Assert.Equal(0, finalGame.Player.Portfolio.ReservedQuantityOf(1));
        Assert.True(finalGame.Player.Portfolio.Cash >= 10050,
            $"player の resting sell 約定で cash が 10050 以上に増えるはず。実際: {finalGame.Player.Portfolio.Cash}");
    }
}
