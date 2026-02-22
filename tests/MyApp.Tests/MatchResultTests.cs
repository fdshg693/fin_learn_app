namespace MyApp.Tests;

using MyApp.Core;

public class MatchResultTests
{
    [Fact]
    public void プロパティが正しく保持される()
    {
        var trade = new TradeResult(1, OrderSide.Buy, 3, 300, 0);
        var book = new OrderBook();
        var result = new MatchResult(trade, book);

        Assert.Equal(trade, result.Trade);
        Assert.Same(book, result.UpdatedBook);
    }
}
