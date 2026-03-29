namespace FinLearn.Core;

/// <summary>
/// 注文（ID・注文者・銘柄・売買区分・注文タイプ・数量・価格）
/// </summary>
public sealed record Order
{
    public int Id { get; }
    public string TraderId { get; }
    public Instrument Instrument { get; }
    public OrderSide Side { get; }
    public OrderType Type { get; }
    public int Quantity { get; }
    public int? Price { get; }
    public int? StopPrice { get; }
    public int CreatedAtTurn { get; }

    /// <summary>
    /// 指値注文を作成する
    /// </summary>
    public Order(int Id, string TraderId, Instrument Instrument, OrderSide Side, int Quantity, int Price, int createdAtTurn = 0)
    {
        if (Quantity <= 0)
            throw new ArgumentException("数量は1以上である必要があります", nameof(Quantity));
        if (Price <= 0)
            throw new ArgumentException("価格は1以上である必要があります", nameof(Price));

        this.Id = Id;
        this.TraderId = TraderId;
        this.Instrument = Instrument;
        this.Side = Side;
        this.Type = OrderType.Limit;
        this.Quantity = Quantity;
        this.Price = Price;
        this.StopPrice = null;
        this.CreatedAtTurn = createdAtTurn;
    }

    private Order(int id, string traderId, Instrument instrument, OrderSide side, OrderType type, int quantity, int? price, int? stopPrice, int createdAtTurn)
    {
        if (quantity <= 0)
            throw new ArgumentException("数量は1以上である必要があります", nameof(quantity));
        if (stopPrice is not null && stopPrice <= 0)
            throw new ArgumentException("ストップ価格は1以上である必要があります", nameof(stopPrice));

        Id = id;
        TraderId = traderId;
        Instrument = instrument;
        Side = side;
        Type = type;
        Quantity = quantity;
        Price = price;
        StopPrice = stopPrice;
        CreatedAtTurn = createdAtTurn;
    }

    /// <summary>
    /// 成行注文を作成する
    /// </summary>
    public static Order CreateMarket(int id, string traderId, Instrument instrument, OrderSide side, int quantity, int? stopPrice = null, int createdAtTurn = 0) =>
        new(id, traderId, instrument, side, OrderType.Market, quantity, null, stopPrice, createdAtTurn);

    internal Order WithQuantity(int quantity) =>
        new(Id, TraderId, Instrument, Side, Type, quantity, Price, StopPrice, CreatedAtTurn);
}
