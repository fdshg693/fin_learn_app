namespace MyApp.Tests;

using MyApp.Core;

/// <summary>
/// 同じIDの銘柄の足し算ができることを確かめるテスト
/// </summary>
public class PositionTests
{
    [Fact]
    public void 同じIDの銘柄を足し算すると数量が合算される()
    {
        var instrument = new Instrument(id: 1, price: 10);
        // Arrange: 同じ銘柄IDの保有を2つ用意
        var position1 = new Position(instrument, quantity: 100); // トヨタ100株
        var position2 = new Position(instrument, quantity: 50);  // トヨタ50株

        var position3 = new Position(instrument, quantity: 150);  // トヨタ150株

        Assert.Equal(position3, position1.Add(position2));
    }

    [Fact]
    public void 価格10円の銘柄を10株保有している場合は代金が100円になる()
    {
        // Arrange: 価格10円の銘柄と10株のポジションを用意
        var instrument = new Instrument(id: 1, price: 10);
        var position = new Position(instrument, quantity: 10);

        // Act: 代金を取得
        var amount = position.Amount;

        // Assert: 10円 × 10株 = 100円
        Assert.Equal(100, amount);
    }
}
