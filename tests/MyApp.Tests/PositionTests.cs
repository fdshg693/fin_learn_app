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
        // Arrange: 同じ銘柄IDの保有を2つ用意
        var position1 = new Position(instrumentId: 1, quantity: 100); // トヨタ100株
        var position2 = new Position(instrumentId: 1, quantity: 50);  // トヨタ50株

        // Act: 足し算する
        var result = position1.Add(position2);

        // Assert: 同じ銘柄として数量が合算されている
        Assert.Equal(1, result.InstrumentId);
        Assert.Equal(150, result.Quantity);
    }
}
