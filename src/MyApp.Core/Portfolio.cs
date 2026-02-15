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
        return _cash + _positionSet.Amount(exchange);
    }

    public int QuantityOf(int instrumentId)
    {
        return _positionSet.QuantityOf(instrumentId);
    }

    public (Portfolio Result, string? Warning) Sell(IExchange exchange, int instrumentId, int quantity)
    {
        return Trade(exchange, instrumentId, quantity, isBuy: false);
    }

    public (Portfolio Result, string? Warning) Buy(IExchange exchange, int instrumentId, int quantity)
    {
        return Trade(exchange, instrumentId, quantity, isBuy: true);
    }

    private (Portfolio Result, string? Warning) Trade(
        IExchange exchange,
        int instrumentId,
        int quantity,
        bool isBuy)
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
        if (!isBuy && totalQuantity < quantity)
        {
            return (this, "保有数量を超えて売却できません");
        }

        var cost = price * quantity;
        if (isBuy && _cash < cost)
        {
            return (this, "現金が不足して購入できません");
        }

        var instrument = isBuy
            ? _positionSet.GetOrCreateInstrument(instrumentId)
            : _positionSet.GetExistingInstrument(instrumentId);
        var newQuantity = isBuy ? totalQuantity + quantity : totalQuantity - quantity;
        var newPositions = _positionSet.SetQuantity(instrumentId, instrument, newQuantity);
        var newCash = _cash + (isBuy ? -cost : cost);

        return (new Portfolio(newCash, newPositions.Positions), null);
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