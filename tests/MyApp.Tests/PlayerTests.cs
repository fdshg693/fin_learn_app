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
        var player = new Player();

        // 約定価格10円 × 3株 = 30円、手数料0
        var trade = new TradeResult(1, OrderSide.Buy, 3, 30, 0);
        var (result, warning) = player.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(9970, result.Portfolio.Cash);
        Assert.Equal(3, result.Portfolio.QuantityOf(instrumentId: 1));
        // 元のプレイヤーは不変
        Assert.Equal(10000, player.Portfolio.Cash);
        Assert.Equal(0, player.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void ポジションを売却できる()
    {
        var player = new Player();

        // まず購入: 10円 × 3株 = 30円
        var buyTrade = new TradeResult(1, OrderSide.Buy, 3, 30, 0);
        var (bought, _) = player.ApplyTrade(buyTrade);
        // 売却: 10円 × 2株 = 20円
        var sellTrade = new TradeResult(1, OrderSide.Sell, 2, 20, 0);
        var (result, warning) = bought.ApplyTrade(sellTrade);

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
        var player = new Player();

        // 10円 × 100株 = 1000円で購入
        var buyTrade = new TradeResult(1, OrderSide.Buy, 100, 1000, 0);
        var (bought, _) = player.ApplyTrade(buyTrade);
        // 株価が10→15に上昇
        var currentExchange = TestData.CreateExchange((1, 15));

        // 評価額 = 現金9000 + 株100×15 = 10500、損益 = 10500 - 10000 = 500
        Assert.Equal(500, bought.ProfitLoss(currentExchange));
    }

    [Fact]
    public void 待つとポートフォリオが変わらない()
    {
        var player = new Player();
        var buyTrade = new TradeResult(1, OrderSide.Buy, 3, 30, 0);
        var (bought, _) = player.ApplyTrade(buyTrade);

        var (result, warning) = bought.Wait();

        Assert.Null(warning);
        Assert.Equal(9970, result.Portfolio.Cash);
        Assert.Equal(3, result.Portfolio.QuantityOf(instrumentId: 1));
        // 元のプレイヤーは不変
        Assert.Equal(9970, bought.Portfolio.Cash);
    }

    // --- 注文生成 ---

    [Fact]
    public void 買い注文を生成できる()
    {
        var player = new Player();
        var instrument = new Instrument(1);

        var order = player.CreateBuyOrder(orderId: 99, instrument, quantity: 5, price: 100);

        Assert.Equal(99, order.Id);
        Assert.Equal("player", order.TraderId);
        Assert.Equal(instrument, order.Instrument);
        Assert.Equal(OrderSide.Buy, order.Side);
        Assert.Equal(5, order.Quantity);
        Assert.Equal(100, order.Price);
    }

    [Fact]
    public void 売り注文を生成できる()
    {
        var player = new Player();
        var instrument = new Instrument(1);

        var order = player.CreateSellOrder(orderId: 42, instrument, quantity: 3, price: 95);

        Assert.Equal(42, order.Id);
        Assert.Equal("player", order.TraderId);
        Assert.Equal(instrument, order.Instrument);
        Assert.Equal(OrderSide.Sell, order.Side);
        Assert.Equal(3, order.Quantity);
        Assert.Equal(95, order.Price);
    }

    // --- 約定結果の適用 ---

    [Fact]
    public void ApplyTradeで買い取引を適用できる()
    {
        var player = new Player();
        var trade = new TradeResult(
            InstrumentId: 1, Side: OrderSide.Buy,
            FilledQuantity: 3, TotalAmount: 300, Fee: 0);

        var (result, warning) = player.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(9700, result.Portfolio.Cash);
        Assert.Equal(3, result.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void ApplyTradeで売り取引を適用できる()
    {
        var player = new Player();
        var buyTrade = new TradeResult(1, OrderSide.Buy, 5, 500, 0);
        var (bought, _) = player.ApplyTrade(buyTrade);

        var sellTrade = new TradeResult(
            InstrumentId: 1, Side: OrderSide.Sell,
            FilledQuantity: 3, TotalAmount: 285, Fee: 0);

        var (result, warning) = bought.ApplyTrade(sellTrade);

        Assert.Null(warning);
        Assert.Equal(9785, result.Portfolio.Cash); // 9500 + 285
        Assert.Equal(2, result.Portfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void ApplyTradeで現金不足の買い取引は警告を返す()
    {
        var player = new Player();
        var trade = new TradeResult(1, OrderSide.Buy, 1, 10001, 0);

        var (result, warning) = player.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(10000, result.Portfolio.Cash);
    }

    [Fact]
    public void ApplyTradeで保有数不足の売り取引は警告を返す()
    {
        var player = new Player();
        var trade = new TradeResult(1, OrderSide.Sell, 1, 100, 0);

        var (result, warning) = player.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
    }

    [Fact]
    public void ApplyTradeで手数料が正しく差し引かれる()
    {
        var player = new Player();
        var trade = new TradeResult(1, OrderSide.Buy, 1, 100, 50);

        var (result, warning) = player.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(9850, result.Portfolio.Cash); // 10000 - 100 - 50
    }

    [Fact]
    public void ApplyTradeは元のプレイヤーを変更しない()
    {
        var player = new Player();
        var trade = new TradeResult(1, OrderSide.Buy, 3, 300, 0);

        var (result, _) = player.ApplyTrade(trade);

        Assert.Equal(10000, player.Portfolio.Cash);
        Assert.Equal(0, player.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal(9700, result.Portfolio.Cash);
    }
}
