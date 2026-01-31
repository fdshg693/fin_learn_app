namespace MyApp.Core;

using System.Collections.Generic;

/// <summary>
/// 複数ポジションの集合
/// </summary>
public sealed class Portfolio
{
    private readonly PositionSet _positionSet;
    private readonly int _cash;

    public Portfolio(int cash, IEnumerable<Position> positions)
    {
        _cash = cash;
        _positionSet = new PositionSet(positions);
    }

    public int Cash => _cash;

    public int TotalAmount(IExchange exchange)
    {
        return _cash + _positionSet.Positions.Sum(position => position.Amount(exchange));
    }

    public int QuantityOf(int instrumentId)
    {
        return _positionSet.Positions
            .Where(position => position.Instrument.Id == instrumentId)
            .Sum(position => position.Quantity);
    }

    public (Portfolio Result, string? Warning) Sell(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
        {
            return (this, "数量は0より大きい必要があります");
        }

        if (!TryGetPrice(exchange, instrumentId, out var price, out var priceWarning))
        {
            return (this, priceWarning);
        }

        var totalQuantity = QuantityOf(instrumentId);
        if (totalQuantity < quantity)
        {
            return (this, "保有数量を超えて売却できません");
        }

        var instrument = _positionSet.Positions
            .First(position => position.Instrument.Id == instrumentId)
            .Instrument;
        var remainingQuantity = totalQuantity - quantity;
        var newPositions = _positionSet.Positions
            .Where(position => position.Instrument.Id != instrumentId)
            .ToList();

        if (remainingQuantity > 0)
        {
            newPositions.Add(new Position(instrument, remainingQuantity));
        }

        var newCash = _cash + price * quantity;
        return (new Portfolio(newCash, newPositions), null);
    }

    public (Portfolio Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        if (quantity <= 0)
        {
            return (this, "数量は0より大きい必要があります");
        }

        if (!TryGetPrice(exchange, instrumentId, out var price, out var priceWarning))
        {
            return (this, priceWarning);
        }

        var instrument = _positionSet.Positions
            .FirstOrDefault(position => position.Instrument.Id == instrumentId)
            ?.Instrument
            ?? new Instrument(instrumentId);
        var cost = price * quantity;
        if (_cash < cost)
        {
            return (this, "現金が不足して購入できません");
        }

        var totalQuantity = QuantityOf(instrumentId) + quantity;
        var newPositions = _positionSet.Positions
            .Where(position => position.Instrument.Id != instrumentId)
            .ToList();
        newPositions.Add(new Position(instrument, totalQuantity));

        var newCash = _cash - cost;
        return (new Portfolio(newCash, newPositions), null);
    }

    private static bool TryGetPrice(
        IExchange exchange,
        int instrumentId,
        out int price,
        out string? warning)
    {
        try
        {
            price = exchange.PriceOf(instrumentId);
        }
        catch (KeyNotFoundException)
        {
            price = 0;
            warning = "価格が取得できません";
            return false;
        }

        if (price <= 0)
        {
            warning = "価格は0より大きい必要があります";
            return false;
        }

        warning = null;
        return true;
    }
}