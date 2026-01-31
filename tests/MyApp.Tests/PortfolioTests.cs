namespace MyApp.Tests;

using MyApp.Core;

public class PortfolioTests
{
    [Fact]
    public void ポジション順序が違ってもポートフォリオは一致する()
    {
        var instrumentA = new Instrument(id: 1, price: 10);
        var instrumentB = new Instrument(id: 2, price: 20);

        var positionA = new Position(instrumentA, quantity: 100);
        var positionB = new Position(instrumentB, quantity: 50);

        var portfolio1 = new Portfolio(new[] { positionA, positionB });
        var portfolio2 = new Portfolio(new[] { positionB, positionA });

        Assert.Equal(portfolio1, portfolio2);
    }
}
