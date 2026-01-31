namespace MyApp.Core;

using System.Collections.ObjectModel;

/// <summary>
/// ポートフォリオ（保有する銘柄の一覧）
/// </summary>
public class Portfolio : IEquatable<Portfolio>
{
    private readonly IReadOnlyList<Position> _positions;

    public Portfolio(IEnumerable<Position> positions)
    {
        _positions = new ReadOnlyCollection<Position>(positions.ToList());
    }

    public bool Equals(Portfolio? other)
    {
        if (other is null)
        {
            return false;
        }

        var thisSorted = _positions
            .OrderBy(x => x.InstrumentId, Comparer<int>.Default)
            .ThenBy(x => x.Quantity)
            .ToList();
        var otherSorted = other._positions
            .OrderBy(x => x.InstrumentId, Comparer<int>.Default)
            .ThenBy(x => x.Quantity)
            .ToList();

        return thisSorted.SequenceEqual(otherSorted, PositionComparer.Instance);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var position in _positions
            .OrderBy(x => x.InstrumentId, Comparer<int>.Default)
            .ThenBy(x => x.Quantity))
        {
            hash.Add(position.InstrumentId);
            hash.Add(position.Quantity);
        }
        return hash.ToHashCode();
    }

    private sealed class PositionComparer : IEqualityComparer<Position>
    {
        public static readonly PositionComparer Instance = new();

        public bool Equals(Position? x, Position? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }
            if (x is null || y is null)
            {
                return false;
            }
            return x.InstrumentId == y.InstrumentId
                && x.Quantity == y.Quantity;
        }

        public int GetHashCode(Position obj)
        {
            var hash = new HashCode();
            hash.Add(obj.InstrumentId);
            hash.Add(obj.Quantity);
            return hash.ToHashCode();
        }
    }
}
