namespace FinLearn.Tests;

using FinLearn.Core;

public class TurnProcessorLoggingTests
{
    private static readonly IReadOnlyList<Instrument> Instruments = new[]
    {
        new Instrument(1), new Instrument(2), new Instrument(3)
    };

    private static readonly IReadOnlyDictionary<int, int> Prices =
        new Dictionary<int, int> { { 1, 100 }, { 2, 200 }, { 3, 300 } };

    private static TurnProcessor CreateProcessor(int seed = 42)
    {
        return new TurnProcessor(new ComputerTrader(new Random(seed)), new NoPriceFluctuator());
    }

    private static Game CreateGame(IReadOnlyDictionary<int, int>? prices = null)
    {
        return new Game(Instruments, prices ?? Prices);
    }

    [Fact]
    public void Wait_はSubmittedOrdersにコンピューター注文のみを含めFillsは空()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Wait(game, fee: 0);

        Assert.Equal(1, result.ProcessedTurn);
        Assert.NotEmpty(result.SubmittedOrders);
        Assert.All(result.SubmittedOrders, o => Assert.Equal("computer", o.TraderId));
        Assert.Empty(result.Fills);
        Assert.Null(result.Warning);
    }

    [Fact]
    public void Buy通常約定_はSubmittedOrdersにコンピューター注文とプレイヤー注文を含む()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 115);

        Assert.Null(result.Warning);
        Assert.Contains(result.SubmittedOrders, o => o.TraderId == "computer");
        Assert.Contains(result.SubmittedOrders, o => o.TraderId == "player");
    }

    [Fact]
    public void Buy通常約定_のFillsはMatchResultの全約定明細と一致する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 115);

        Assert.Null(result.Warning);
        Assert.NotEmpty(result.Fills);

        var playerOrder = result.SubmittedOrders.Single(o => o.TraderId == "player");
        var playerFill = result.Fills.Single(f => f.OrderId == playerOrder.Id);
        Assert.Equal(result.Trade!.FilledQuantity, playerFill.FilledQuantity);
        Assert.Equal(result.Trade.TotalAmount, playerFill.TotalAmount);

        Assert.Contains(result.Fills, f => f.OrderId != playerOrder.Id);
    }

    [Fact]
    public void Buy引数バリデーション失敗_はSubmittedOrdersもFillsも空()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, result.Warning);
        Assert.Equal(1, result.ProcessedTurn);
        Assert.Empty(result.SubmittedOrders);
        Assert.Empty(result.Fills);
    }

    [Fact]
    public void Buy成行約定ゼロ_はFillsが空でSubmittedOrdersはコンピューターのみ()
    {
        var game = CreateGame();
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1);

        Assert.Equal(Messages.NoMatchingSellOrders, result.Warning);
        Assert.Single(result.SubmittedOrders);
        Assert.Equal("player", result.SubmittedOrders[0].TraderId);
        Assert.Empty(result.Fills);
    }

    [Fact]
    public void Buy残高不足_はFillsを空にロールバックする()
    {
        var seedBook = new OrderBook().Add(
            new Order(Id: 1, TraderId: "computer", Instrument: Instruments[0],
                Side: OrderSide.Sell, Quantity: 1, Price: 20000, createdAtTurn: 1));
        var game = new Game(
            new Player(),
            turn: 1,
            seedBook,
            nextOrderId: 2,
            Instruments,
            Prices);
        var processor = new TurnProcessor(new NoOpOrderPlacer(), new NoPriceFluctuator());

        var result = processor.Buy(game, fee: 0, instrumentId: 1, quantity: 1, price: 20000);

        Assert.Equal(Messages.InsufficientCashToBuy, result.Warning);
        Assert.Single(result.SubmittedOrders);
        Assert.Equal("player", result.SubmittedOrders[0].TraderId);
        Assert.Empty(result.Fills);
    }

    [Fact]
    public void ProcessedTurnは入力ゲームのターンと一致する()
    {
        var game = CreateGame();
        var processor = CreateProcessor();

        var afterWait = processor.Wait(game, fee: 0);
        var afterBuy = processor.Buy(afterWait.Game, fee: 0, instrumentId: 1, quantity: 1, price: 115);

        Assert.Equal(1, afterWait.ProcessedTurn);
        Assert.Equal(2, afterBuy.ProcessedTurn);
    }
}
