namespace MyApp.Core;

/// <summary>
/// 複数ポジションの集合（順序を無視して一致判定する）
/// </summary>
public sealed class Portfolio : IEquatable<Portfolio>
{
    private readonly IReadOnlyList<Position> _positions;

    public Portfolio(IEnumerable<Position> positions)
    {
        _positions = positions.ToList().AsReadOnly();
    }

    public IReadOnlyList<Position> Positions => _positions;

    public bool Equals(Portfolio? other)
    {
        if (other is null)
        {
            return false;
        }

        return _positions
            .OrderBy(position => position.Instrument.Id)
            .ThenBy(position => position.Quantity)
            .SequenceEqual(
                other._positions
                    .OrderBy(position => position.Instrument.Id)
                    .ThenBy(position => position.Quantity));
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Portfolio);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var position in _positions
            .OrderBy(position => position.Instrument.Id)
            .ThenBy(position => position.Quantity))
        {
            hash.Add(position);
        }
        return hash.ToHashCode();
    }
}