namespace MyApp.Core;

using System.Collections.Generic;

/// <summary>
/// 銘柄の保有（銘柄IDと数量）
/// </summary>
public class Position : IEquatable<Position>
{
    public Position(Instrument instrument, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "数量は0より大きい必要があります");
        }
        Instrument = instrument;
        Quantity = quantity;
    }

    public int Quantity { get; }
    public Instrument Instrument { get; }
    public int Amount(IExchange exchange)
    {
        if (!TryGetPrice(exchange, Instrument.Id, out var price))
        {
            return 0;
        }
        return price * Quantity;
    }

    public static PositionSet operator +(Position left, Position right)
    {
        return new PositionSet(new[] { left, right });
    }

    public bool Equals(Position? other)
    {
        if (other is null)
        {
            return false;
        }
        return Instrument.Id == other.Instrument.Id
            && Quantity == other.Quantity;
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Position);
    }
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Instrument.Id);
        hash.Add(Quantity);
        return hash.ToHashCode();
    }

    private static bool TryGetPrice(IExchange exchange, int instrumentId, out int price)
    {
        try
        {
            price = exchange.PriceOf(instrumentId);
        }
        catch (KeyNotFoundException)
        {
            price = 0;
            return false;
        }
        return price > 0;
    }
}
