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

    public (Portfolio Result, string? Warning) Buy(int instrumentId, int filledQuantity, int totalCost, int fee)
    {
        if (filledQuantity <= 0)
            return (this, Messages.QuantityMustBePositive);
        if (_cash < totalCost + fee)
            return (this, Messages.InsufficientCashToBuy);

        var instrument = _positionSet.GetOrCreateInstrument(instrumentId);
        var newQuantity = QuantityOf(instrumentId) + filledQuantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _cash - totalCost - fee;
        return (new Portfolio(newCash, newPositions.Positions), null);
    }

    public (Portfolio Result, string? Warning) Sell(int instrumentId, int filledQuantity, int totalProceeds, int fee)
    {
        if (filledQuantity <= 0)
            return (this, Messages.QuantityMustBePositive);
        var totalQuantity = QuantityOf(instrumentId);
        if (totalQuantity < filledQuantity)
            return (this, Messages.InsufficientQuantityToSell);

        var instrument = _positionSet.GetExistingInstrument(instrumentId);
        var newQuantity = totalQuantity - filledQuantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _cash + totalProceeds - fee;
        return (new Portfolio(newCash, newPositions.Positions), null);
    }
}
