namespace MyApp.Core;

/// <summary>
/// 銘柄の保有（銘柄IDと数量）
/// </summary>
public sealed record Position
{
    public Position(Instrument instrument, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), Messages.QuantityMustBePositive);
        }
        Instrument = instrument;
        Quantity = quantity;
    }

    public int Quantity { get; }
    public Instrument Instrument { get; }

    public int Amount(IExchange exchange)
    {
        if (!exchange.TryGetPrice(Instrument.Id, out var price, out _))
        {
            return 0;
        }
        return price * Quantity;
    }

    public static PositionSet operator +(Position left, Position right)
    {
        return new PositionSet(new[] { left, right });
    }
}
