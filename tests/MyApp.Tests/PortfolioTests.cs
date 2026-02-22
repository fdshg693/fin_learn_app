namespace MyApp.Tests;

using MyApp.Core;

public class PortfolioTests
{
    [Fact]
    public void 現金プロパティを保持できる()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        Assert.Equal(1000, portfolio.Cash);
    }

    [Fact]
    public void 現金とポジションの合計を計算できる()
    {
        var exchange = TestData.CreateExchange((1, 10), (2, 20));

        var positionA = new Position(TestData.Instrument1, quantity: 10);
        var positionB = new Position(TestData.Instrument2, quantity: 5);

        var portfolio = new Portfolio(cash: 1000, positions: new[] { positionA, positionB });

        var totalAmount = portfolio.TotalAmount(exchange);

        Assert.Equal(1200, totalAmount);
    }

    [Fact]
    public void 銘柄IDを指定して保有数量を取得できる()
    {
        var positionA1 = new Position(TestData.Instrument1, quantity: 10);
        var positionA2 = new Position(TestData.Instrument1, quantity: 5);
        var positionB = new Position(TestData.Instrument2, quantity: 3);

        var portfolio = new Portfolio(cash: 0, positions: new[] { positionA1, positionA2, positionB });

        var quantity = portfolio.QuantityOf(instrumentId: 1);

        Assert.Equal(15, quantity);
    }

    [Fact]
    public void 保有数量を超える売却は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 10, TotalAmount: 100, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 保有数量以内の売却は現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 8);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        // 約定価格10円 × 3株 = 30円
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(1030, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲内の購入は現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });

        // 約定価格10円 × 3株 = 30円
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(70, resultPortfolio.Cash);
        Assert.Equal(8, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲外の購入は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 20, positions: new[] { position });

        // 約定価格10円 × 3株 = 30 > 現金20
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(20, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 未保有銘柄の購入は許可され現金と数量が更新される()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 200, positions: new[] { position });

        // 約定価格20円 × 3株 = 60円
        var trade = new TradeResult(InstrumentId: 2, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 60, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(140, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 2));
    }

    [Fact]
    public void Sellの数量が0以下の場合は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 0, TotalAmount: 0, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void Buyの数量が0以下の場合は警告して何もしない()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: -1, TotalAmount: 0, Fee: 0);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 購入時に手数料が差し引かれる()
    {
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        // 約定価格10円 × 3株 = 30 + 手数料50 = 80
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(920, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 売却時に手数料が差し引かれる()
    {
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        // 現金1000 + 約定価格10円 × 3株 = 30 - 手数料50 = 980
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Sell, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Null(warning);
        Assert.Equal(980, resultPortfolio.Cash);
        Assert.Equal(2, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 手数料込みで現金が不足する購入は警告して何もしない()
    {
        var portfolio = new Portfolio(cash: 70, positions: new Position[] { });

        // 約定価格10円 × 3株 = 30 + 手数料50 = 80 > 現金70
        var trade = new TradeResult(InstrumentId: 1, Side: OrderSide.Buy, FilledQuantity: 3, TotalAmount: 30, Fee: 50);
        var (resultPortfolio, warning) = portfolio.ApplyTrade(trade);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(70, resultPortfolio.Cash);
    }
}
