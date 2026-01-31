namespace MyApp.Tests;

using System.Collections.Generic;
using MyApp.Core;

/// <summary>
/// 同じIDの銘柄の足し算ができることを確かめるテスト
/// </summary>
public class PositionTests
{
    [Fact]
    public void 価格10円の銘柄を10株保有している場合は代金が100円になる()
    {
        // Arrange: 価格10円の銘柄と10株のポジションを用意
        var instrument = new Instrument(id: 1);
        var position = new Position(instrument, quantity: 10);
        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 10 },
        });

        // Act: 代金を取得
        var amount = position.Amount(exchange);

        // Assert: 10円 × 10株 = 100円
        Assert.Equal(100, amount);
    }
}
