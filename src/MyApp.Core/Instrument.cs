namespace MyApp.Core;

/// <summary>
/// 銘柄（ID）
/// </summary>
public class Instrument : IEquatable<Instrument>
{
    public Instrument(int id)
    {
        Id = id;
    }

    public int Id { get; }

    public bool Equals(Instrument? other)
    {
        if (other is null)
        {
            return false;
        }
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Instrument);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
