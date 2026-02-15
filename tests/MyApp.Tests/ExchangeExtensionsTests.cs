namespace MyApp.Tests;

using System.Collections.Generic;
using MyApp.Core;

public class ExchangeExtensionsTests
{
    [Fact]
    public void 登録済み銘柄の価格を取得できる()
    {
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 100 },
        });

        var result = exchange.TryGetPrice(instrumentId: 1, out var price, out var warning);

        Assert.True(result);
        Assert.Equal(100, price);
        Assert.Null(warning);
    }

    [Fact]
    public void 未登録銘柄は失敗して警告を返す()
    {
        var exchange = new TestExchange(new Dictionary<int, int>());

        var result = exchange.TryGetPrice(instrumentId: 999, out var price, out var warning);

        Assert.False(result);
        Assert.Equal(0, price);
        Assert.Equal("価格が取得できません", warning);
    }

    [Fact]
    public void 価格が0以下の場合は失敗して警告を返す()
    {
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 0 },
        });

        var result = exchange.TryGetPrice(instrumentId: 1, out var price, out var warning);

        Assert.False(result);
        Assert.Equal("価格は0より大きい必要があります", warning);
    }
}
