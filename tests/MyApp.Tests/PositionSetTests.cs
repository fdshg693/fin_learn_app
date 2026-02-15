namespace MyApp.Tests;

using MyApp.Core;

public class PositionSetTests
{
    [Fact]
    public void ポジション順序が違ってもポジション集合は一致する()
    {
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);

        var positionA = new Position(instrumentA, quantity: 100);
        var positionB = new Position(instrumentB, quantity: 50);

        var positionSet1 = new PositionSet(new[] { positionA, positionB });
        var positionSet2 = new PositionSet(new[] { positionB, positionA });

        Assert.Equal(positionSet1, positionSet2);
    }

    [Fact]
    public void 同じIDの銘柄を足し合わせた上でポジション集合の一致を確かめられる()
    {
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);

        var positionA = new Position(instrumentA, quantity: 50);
        var positionB = new Position(instrumentA, quantity: 100);
        var positionC = new Position(instrumentB, quantity: 100);

        var positionSet1 = new PositionSet(new[] { positionA, positionA, positionC });
        var positionSet2 = new PositionSet(new[] { positionB, positionC });

        Assert.Equal(positionSet1, positionSet2);
    }

    [Fact]
    public void ポジションの足し算はPositionSetを返す()
    {
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);

        var positionA = new Position(instrumentA, quantity: 100);
        var positionB = new Position(instrumentB, quantity: 50);

        var expected = new PositionSet(new[] { positionA, positionB });

        var result1 = positionA + positionB;
        var result2 = new PositionSet(new[] { positionA }) + positionB;
        var result3 = positionA + new PositionSet(new[] { positionB });

        Assert.IsType<PositionSet>(result1);
        Assert.IsType<PositionSet>(result2);
        Assert.IsType<PositionSet>(result3);
        Assert.Equal(expected, result1);
        Assert.Equal(expected, result2);
        Assert.Equal(expected, result3);
    }

    [Fact]
    public void ポジション集合の評価額を取得できる()
    {
        var instrumentA = new Instrument(id: 1);
        var instrumentB = new Instrument(id: 2);

        var positionA = new Position(instrumentA, quantity: 100);
        var positionB = new Position(instrumentB, quantity: 50);

        var positionSet = new PositionSet(new[] { positionA, positionB });

        var exchange = new TestExchange(new Dictionary<int, int>
        {
            { 1, 1000 },  // 銘柄1の価格: 1000
            { 2, 2000 }   // 銘柄2の価格: 2000
        });

        var amount = positionSet.Amount(exchange);

        // 100 * 1000 + 50 * 2000 = 200,000
        Assert.Equal(200000, amount);
    }
}
