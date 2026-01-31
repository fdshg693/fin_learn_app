namespace MyApp.Tests;

using MyApp.Core;

/// <summary>
/// 異なる銘柄の配列を持つポートフォリオの一致を確かめるテスト
/// </summary>
public class PortfolioTests
{
    [Fact]
    public void 銘柄の順序が異なってもポートフォリオは一致する()
    {
        // Arrange: 異なる順序の銘柄配列を持つポートフォリオを用意
        var portfolio1 = new Portfolio(new[]
        {
            new Position(instrumentId: 1, quantity: 1),
            new Position(instrumentId: 2, quantity: 1),
        });
        var portfolio2 = new Portfolio(new[]
        {
            new Position(instrumentId: 2, quantity: 1),
            new Position(instrumentId: 1, quantity: 1),
        });

        // Act & Assert: 順序が異なっても一致する
        Assert.Equal(portfolio1, portfolio2);
    }
    [Fact]
    public void 同一銘柄の合計が同じであればポートフォリオは一致する()
    {
        // Arrange: 同一銘柄の合計が同じであるポートフォリオを用意
        var portfolio1 = new Portfolio(new[]
        {
            new Position(instrumentId: 1, quantity: 1),
            new Position(instrumentId: 1, quantity: 1),
        });
        var portfolio2 = new Portfolio(new[]
        {
            new Position(instrumentId: 1, quantity: 2),
        });

        // Act & Assert: 同一銘柄の合計が同じであれば一致する
        Assert.Equal(portfolio1, portfolio2);
    }
}
