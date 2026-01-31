namespace MyApp.Tests;

using System.Collections.Generic;
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
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
            { 2, 20 },
        });

        var positionA = new Position(instrumentA, quantity: 10);
        var positionB = new Position(instrumentB, quantity: 5);

        var portfolio = new Portfolio(cash: 1000, positions: new[] { positionA, positionB });

        var totalAmount = portfolio.TotalAmount(exchange);

        Assert.Equal(1200, totalAmount);
    }

    [Fact]
    public void 銘柄IDを指定して保有数量を取得できる()
    {
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);

        var positionA1 = new Position(instrumentA, quantity: 10);
        var positionA2 = new Position(instrumentA, quantity: 5);
        var positionB = new Position(instrumentB, quantity: 3);

        var portfolio = new Portfolio(cash: 0, positions: new[] { positionA1, positionA2, positionB });

        var quantity = portfolio.QuantityOf(instrumentId: 1);

        Assert.Equal(15, quantity);
    }

    [Fact]
    public void 保有数量を超える売却は警告して何もしない()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 10);

        Assert.Equal("保有数量を超えて売却できません", warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 保有数量以内の売却は現金と数量が更新される()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 8);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(1030, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲内の購入は現金と数量が更新される()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 100, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(70, resultPortfolio.Cash);
        Assert.Equal(8, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 現金の範囲外の購入は警告して何もしない()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 20, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: 3);

        Assert.Equal("現金が不足して購入できません", warning);
        Assert.Equal(20, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void 未保有銘柄の購入は許可され現金と数量が更新される()
    {
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
            { 2, 20 },
        });
        var instrument = new Instrument(id: 1);
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 200, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 2, quantity: 3);

        Assert.Null(warning);
        Assert.Equal(140, resultPortfolio.Cash);
        Assert.Equal(3, resultPortfolio.QuantityOf(instrumentId: 2));
    }

    [Fact]
    public void Sellの数量が0以下の場合は警告して何もしない()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Sell(exchange, instrumentId: 1, quantity: 0);

        Assert.Equal("数量は0より大きい必要があります", warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }

    [Fact]
    public void Buyの数量が0以下の場合は警告して何もしない()
    {
        var instrument = new Instrument(id: 1);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });
        var position = new Position(instrument, quantity: 5);
        var portfolio = new Portfolio(cash: 1000, positions: new[] { position });

        var (resultPortfolio, warning) = portfolio.Buy(exchange, instrumentId: 1, quantity: -1);

        Assert.Equal("数量は0より大きい必要があります", warning);
        Assert.Equal(1000, resultPortfolio.Cash);
        Assert.Equal(5, resultPortfolio.QuantityOf(instrumentId: 1));
    }
}
