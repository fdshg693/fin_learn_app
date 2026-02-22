namespace MyApp.Tests;

using MyApp.Core;

public class SimpleExchangeTests
{
    [Fact]
    public void 登録済み銘柄の価格を取得できる()
    {
        var prices = new Dictionary<int, int> { { 1, 100 }, { 2, 200 } };
        var exchange = new SimpleExchange(prices);

        Assert.True(exchange.TryGetPrice(1, out var price1));
        Assert.Equal(100, price1);
        Assert.True(exchange.TryGetPrice(2, out var price2));
        Assert.Equal(200, price2);
    }

    [Fact]
    public void 未登録銘柄はfalseを返す()
    {
        var prices = new Dictionary<int, int> { { 1, 100 } };
        var exchange = new SimpleExchange(prices);

        Assert.False(exchange.TryGetPrice(99, out var price));
        Assert.Equal(0, price);
    }

    [Fact]
    public void 価格が0以下の場合はfalseを返す()
    {
        var prices = new Dictionary<int, int> { { 1, 0 }, { 2, -10 } };
        var exchange = new SimpleExchange(prices);

        Assert.False(exchange.TryGetPrice(1, out _));
        Assert.False(exchange.TryGetPrice(2, out _));
    }

    [Fact]
    public void 手数料を取得できる()
    {
        var prices = new Dictionary<int, int> { { 1, 100 } };
        var exchange = new SimpleExchange(prices, fee: 50);

        Assert.Equal(50, exchange.Fee);
    }

    [Fact]
    public void デフォルト手数料は0()
    {
        var prices = new Dictionary<int, int> { { 1, 100 } };
        var exchange = new SimpleExchange(prices);

        Assert.Equal(0, exchange.Fee);
    }
}
