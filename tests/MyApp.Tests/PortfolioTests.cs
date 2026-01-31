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
        var instrumentA = new Instrument(id: 1, price: 10);
        var instrumentB = new Instrument(id: 2, price: 20);

        var positionA = new Position(instrumentA, quantity: 10);
        var positionB = new Position(instrumentB, quantity: 5);

        var portfolio = new Portfolio(cash: 1000, positions: new[] { positionA, positionB });

        var totalAmount = portfolio.TotalAmount();

        Assert.Equal(1200, totalAmount);
    }
}
