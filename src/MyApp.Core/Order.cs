namespace MyApp.Core;

/// <summary>
/// 注文（ID・注文者・銘柄・売買区分・数量・価格）
/// </summary>
public sealed record Order
{
    public int Id { get; }
    public string TraderId { get; }
    public Instrument Instrument { get; }
    public OrderSide Side { get; }
    public int Quantity { get; }
    public int Price { get; }

    public Order(int Id, string TraderId, Instrument Instrument, OrderSide Side, int Quantity, int Price)
    {
        if (Quantity <= 0)
            throw new ArgumentException("数量は1以上である必要があります", nameof(Quantity));
        if (Price <= 0)
            throw new ArgumentException("価格は1以上である必要があります", nameof(Price));

        this.Id = Id;
        this.TraderId = TraderId;
        this.Instrument = Instrument;
        this.Side = Side;
        this.Quantity = Quantity;
        this.Price = Price;
    }

    internal Order WithQuantity(int quantity) =>
        new(Id, TraderId, Instrument, Side, quantity, Price);
}
