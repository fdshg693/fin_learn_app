namespace MyApp.Core;

/// <summary>
/// 複数ポジションの集合（順序を無視して一致判定する）
/// </summary>
public sealed class Portfolio : IEquatable<Portfolio>
{
    private readonly IReadOnlyList<Position> _positions;
    private readonly PositionSet _positionSet;

    public Portfolio(IEnumerable<Position> positions)
    {
        _positions = positions.ToList().AsReadOnly();
        _positionSet = new PositionSet(_positions);
    }

    public IReadOnlyList<Position> Positions => _positions;

    public bool Equals(Portfolio? other)
    {
        if (other is null)
        {
            return false;
        }
        return _positionSet.Equals(other._positionSet);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Portfolio);
    }

    public override int GetHashCode()
    {
        return _positionSet.GetHashCode();
    }
}