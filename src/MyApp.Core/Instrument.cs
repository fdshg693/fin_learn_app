namespace MyApp.Core;

/// <summary>
/// 銘柄（ID）
/// </summary>
public class Instrument
{
    public Instrument(int id)
    {
        Id = id;
    }

    public int Id { get; }
}
