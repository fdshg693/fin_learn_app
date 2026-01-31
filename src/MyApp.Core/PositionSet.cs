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

    private static IReadOnlyList<Position> Normalize(IEnumerable<Position> positions)
    {
        return positions
            .GroupBy(position => position.Instrument.Id)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var first = group.First();
                var totalQuantity = group.Sum(position => position.Quantity);
                return new Position(first.Instrument, totalQuantity);
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
