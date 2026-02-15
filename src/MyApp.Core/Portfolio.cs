namespace MyApp.Core;

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
            return (this, Messages.QuantityMustBePositive);
        }

        if (!exchange.TryGetPrice(instrumentId, out var price, out var priceWarning))
        {
            return (this, priceWarning);
        }

        var totalQuantity = QuantityOf(instrumentId);
        if (!isBuy && totalQuantity < quantity)
        {
            return (this, Messages.InsufficientQuantityToSell);
        }

        var cost = price * quantity;
        var fee = exchange.Fee;
        if (isBuy && _cash < cost + fee)
        {
            return (this, Messages.InsufficientCashToBuy);
        }

        var instrument = isBuy
            ? _positionSet.GetOrCreateInstrument(instrumentId)
            : _positionSet.GetExistingInstrument(instrumentId);
        var newQuantity = isBuy ? totalQuantity + quantity : totalQuantity - quantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _cash + (isBuy ? -(cost + fee) : cost - fee);

        return (new Portfolio(newCash, newPositions.Positions), null);
    }
}