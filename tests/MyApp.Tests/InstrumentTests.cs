namespace MyApp.Tests;

using MyApp.Core;

public class InstrumentTests
{
    [Fact]
    public void 同じIDのInstrumentは等しい()
    {
        var a = new Instrument(id: 1);
        var b = new Instrument(id: 1);

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
    }

    [Fact]
    public void 異なるIDのInstrumentは等しくない()
    {
        var a = new Instrument(id: 1);
        var b = new Instrument(id: 2);

        Assert.NotEqual(a, b);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void 同じIDのInstrumentは同じHashCodeを返す()
    {
        var a = new Instrument(id: 1);
        var b = new Instrument(id: 1);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void nullとは等しくない()
    {
        var a = new Instrument(id: 1);

        Assert.False(a.Equals(null));
    }
}
