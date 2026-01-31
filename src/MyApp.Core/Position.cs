namespace MyApp.Core;

/// <summary>
/// 銘柄の保有（銘柄IDと数量）
/// </summary>
public class Position : IEquatable<Position>
{
    public Position(Instrument instrument, int quantity)
    {
        Instrument = instrument;
        Quantity = quantity;
    }

    public int Quantity { get; }
    public Instrument Instrument { get; }
    public int Amount => Instrument.Price * Quantity;

    public static PositionSet operator +(Position left, Position right)
    {
        return new PositionSet(new[] { left, right });
    }

    public bool Equals(Position? other)
    {
        if (other is null)
        {
            return false;
        }
        return Instrument.Id == other.Instrument.Id
            && Quantity == other.Quantity;
    }
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Instrument.Id);
        hash.Add(Quantity);
        return hash.ToHashCode();
    }
}
