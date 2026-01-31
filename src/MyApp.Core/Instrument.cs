namespace MyApp.Core;

/// <summary>
/// 銘柄（IDと価格）
/// </summary>
public class Instrument
{
    public Instrument(int id, int price)
    {
        Id = id;
        Price = price;
    }

    public int Id { get; }
    public int Price { get; }
}
