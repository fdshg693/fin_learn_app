namespace FinLearn.Tests;

using FinLearn.Core;

public class ExchangeTests
{
    [Fact]
    public void 登録済み銘柄の価格を取得できる()
    {
        var exchange = TestData.CreateExchange((1, 100));

        var result = exchange.TryGetPrice(instrumentId: 1, out var price);

        Assert.True(result);
        Assert.Equal(100, price);
    }

    [Fact]
    public void 未登録銘柄はfalseを返す()
    {
        var exchange = TestData.CreateExchange();

        var result = exchange.TryGetPrice(instrumentId: 999, out var price);

        Assert.False(result);
        Assert.Equal(0, price);
    }

    [Fact]
    public void 価格が0以下の場合はfalseを返す()
    {
        var exchange = TestData.CreateExchange((1, 0));

        var result = exchange.TryGetPrice(instrumentId: 1, out var price);

        Assert.False(result);
        Assert.Equal(0, price);
    }
}
