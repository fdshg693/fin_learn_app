namespace MyApp.Core;

/// <summary>
/// 異なる銘柄を含むポジション集合（銘柄IDごとに数量を合算する）
/// </summary>
public sealed class PositionSet : IEquatable<PositionSet>
{
    private readonly IReadOnlyList<Position> _positions;

    public PositionSet(IEnumerable<Position> positions)
    {
        _positions = Normalize(positions);
    }

    public IReadOnlyList<Position> Positions => _positions;

    public int QuantityOf(int instrumentId)
    {
        return _positions
            .Where(position => position.Instrument.Id == instrumentId)
            .Sum(position => position.Quantity);
    }

    public Instrument GetExistingInstrument(int instrumentId)
    {
        return _positions
            .First(position => position.Instrument.Id == instrumentId)
            .Instrument;
    }

    public Instrument GetOrCreateInstrument(int instrumentId)
    {
        return _positions
            .FirstOrDefault(position => position.Instrument.Id == instrumentId)
            ?.Instrument
            ?? new Instrument(instrumentId);
    }

    public PositionSet SetQuantity(Instrument instrument, int quantity)
    {
        var newPositions = _positions
            .Where(position => position.Instrument.Id != instrument.Id)
            .ToList();

        if (quantity > 0)
        {
            newPositions.Add(new Position(instrument, quantity));
        }

        return new PositionSet(newPositions);
    }

    public PositionSet Add(Position position)
    {
        return new PositionSet(_positions.Append(position));
    }

    public PositionSet Add(PositionSet other)
    {
        return new PositionSet(_positions.Concat(other._positions));
    }

    public static PositionSet operator +(PositionSet left, Position right)
    {
        return left.Add(right);
    }

    public static PositionSet operator +(Position left, PositionSet right)
    {
        return right.Add(left);
    }

    public int Amount(IExchange exchange)
    {
        return _positions.Sum(position => position.Amount(exchange));
    }

    private static IReadOnlyList<Position> Normalize(IEnumerable<Position> positions)
    {
        return positions
            .GroupBy(position => position.Instrument)
            .OrderBy(group => group.Key.Id)
            .Select(group =>
            {
                var totalQuantity = group.Sum(position => position.Quantity);
                return new Position(group.Key, totalQuantity);
            })
            .ToList()
            .AsReadOnly();
    }

    public bool Equals(PositionSet? other)
    {
        if (other is null)
        {
            return false;
        }
        return _positions.SequenceEqual(other._positions);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var position in _positions)
        {
            hash.Add(position);
        }
        return hash.ToHashCode();
    }
}
