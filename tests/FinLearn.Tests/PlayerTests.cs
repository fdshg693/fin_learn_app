namespace FinLearn.Tests;

using FinLearn.Core;

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
    public void デフォルト名はplayerである()
    {
        var player = new Player();

        Assert.Equal("player", player.Name);
    }

    // --- WithPortfolio ---

    [Fact]
    public void WithPortfolioで新しいポートフォリオを持つプレイヤーを返す()
    {
        var player = new Player();
        var newPortfolio = new Portfolio(cash: 5000, positions: new[] { new Position(TestData.Instrument1, 10) });

        var result = player.WithPortfolio(newPortfolio);

        Assert.Equal(5000, result.Portfolio.Cash);
        Assert.Equal(10, result.Portfolio.QuantityOf(instrumentId: 1));
        Assert.Equal("player", result.Name);
    }

    [Fact]
    public void WithPortfolioは元のプレイヤーを変更しない()
    {
        var player = new Player();
        var newPortfolio = new Portfolio(cash: 5000, positions: Array.Empty<Position>());

        var result = player.WithPortfolio(newPortfolio);

        Assert.Equal(10000, player.Portfolio.Cash);
        Assert.Equal(5000, result.Portfolio.Cash);
    }

    // --- 損益計算 ---

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
        var (resultPortfolio, _) = player.Portfolio.ApplyTrade(buyTrade);
        var bought = player.WithPortfolio(resultPortfolio);

        // 株価が10→15に上昇
        var currentExchange = TestData.CreateExchange((1, 15));

        // 評価額 = 現金9000 + 株100×15 = 10500、損益 = 10500 - 10000 = 500
        Assert.Equal(500, bought.ProfitLoss(currentExchange));
    }

    // --- 注文生成 ---

    [Fact]
    public void 買い注文を生成できる()
    {
        var player = new Player();
        var instrument = new Instrument(1);

        var order = player.CreateOrder(orderId: 99, instrument, OrderSide.Buy, quantity: 5, price: 100, stopPrice: null, createdAtTurn: 0, expiresAtTurn: 2);

        Assert.Equal(99, order.Id);
        Assert.Equal("player", order.TraderId);
        Assert.Equal(instrument, order.Instrument);
        Assert.Equal(OrderSide.Buy, order.Side);
        Assert.Equal(5, order.Quantity);
        Assert.Equal(100, order.Price);
    }

    [Fact]
    public void 成行注文を生成できる()
    {
        var player = new Player();
        var instrument = new Instrument(1);

        var order = player.CreateOrder(orderId: 10, instrument, OrderSide.Buy, quantity: 5, price: null, stopPrice: null, createdAtTurn: 0, expiresAtTurn: 2);

        Assert.Equal(10, order.Id);
        Assert.Equal("player", order.TraderId);
        Assert.Equal(instrument, order.Instrument);
        Assert.Equal(OrderSide.Buy, order.Side);
        Assert.Equal(OrderType.Market, order.Type);
        Assert.Equal(5, order.Quantity);
        Assert.Null(order.Price);
    }
}
