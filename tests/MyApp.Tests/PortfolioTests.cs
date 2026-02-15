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
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 10);

        Assert.Equal(Messages.InsufficientQuantityToSell, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 保有数量以内の売却は現金と数量が更新される()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 8);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(1030, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲内の購入は現金と数量が更新される()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(70, resultPortfolio.Cash);
        Assert.Equal(8, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲外の購入は警告して何もしない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 20, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(20, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 未保有銘柄の購入は許可され現金と数量が更新される()
    {
        var exchange = TestData.CreateExchange((1, 10), (2, 20));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 200, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 2, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(140, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 2));
    }

    [Fact]
    public void Sellの数量が0以下の場合は警告して何もしない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 0);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void Buyの数量が0以下の場合は警告して何もしない()
    {
        var exchange = TestData.CreateExchange((1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: -1);

        Assert.Equal(Messages.QuantityMustBePositive, warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 購入時に手数料が差し引かれる()
    {
        var exchange = TestData.CreateExchange(fee: 50, (1, 10));
        var portfolio = new Portfolio(cash: 1000, positions: new Position[] { });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        // 株価10 × 3株 = 30 + 手数料50 = 80
        Assert.Equal(920, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 売却時に手数料が差し引かれる()
    {
        var exchange = TestData.CreateExchange(fee: 50, (1, 10));
        var position = new Position(TestData.Instrument1, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        // 現金1000 + 株価10 × 3株 = 30 - 手数料50 = 980
        Assert.Equal(980, resultPortfolio.Cash);
        Assert.Equal(2, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 手数料込みで現金が不足する購入は警告して何もしない()
    {
        var exchange = TestData.CreateExchange(fee: 50, (1, 10));
        var portfolio = new Portfolio(cash: 70, positions: new Position[] { });

        // 株価10 × 3株 = 30 + 手数料50 = 80 > 現金70
        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Equal(Messages.InsufficientCashToBuy, warning);
        Assert.Equal(70, resultPortfolio.Cash);
    }
}
