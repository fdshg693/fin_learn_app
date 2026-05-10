namespace FinLearn.Core;

/// <summary>
/// 複数ポジションの集合
/// </summary>
public sealed class Portfolio
{
    private readonly PositionSet _positionSet;
    private readonly int _cash;
    private readonly bool _isInfinite;

    public Portfolio(int cash, IEnumerable<Position> positions)
        : this(cash, positions, isInfinite: false)
    {
    }

    private Portfolio(int cash, IEnumerable<Position> positions, bool isInfinite)
    {
        _cash = cash;
        _positionSet = new PositionSet(positions);
        _isInfinite = isInfinite;
    }

    /// <summary>
    /// コンピュータートレーダー用の「∞」ポートフォリオ。
    /// 現金・保有数量は概念上無限で、Buy/Sell 適用時は不変（Apply はノーオペ）。
    /// </summary>
    public static Portfolio CreateInfinite() =>
        new Portfolio(cash: int.MaxValue, positions: Array.Empty<Position>(), isInfinite: true);

    public int Cash => _cash;
    public bool IsInfinite => _isInfinite;

    public int TotalAmount(IExchange exchange)
    {
        return _cash + _positionSet.Amount(exchange);
    }

    public int QuantityOf(int instrumentId)
    {
        return _positionSet.QuantityOf(instrumentId);
    }

    public (Portfolio Result, string? Warning) ApplyTrade(TradeResult trade)
    {
        if (_isInfinite)
            return (this, null);

        return trade.Side switch
        {
            OrderSide.Buy => Buy(trade),
            OrderSide.Sell => Sell(trade),
            _ => throw new ArgumentOutOfRangeException(nameof(trade))
        };
    }

    private (Portfolio Result, string? Warning) Buy(TradeResult trade)
    {
        if (trade.FilledQuantity <= 0)
            return (this, Messages.QuantityMustBePositive);
        if (_cash < trade.TotalAmount + trade.Fee)
            return (this, Messages.InsufficientCashToBuy);

        var instrument = _positionSet.GetOrCreateInstrument(trade.InstrumentId);
        var newQuantity = QuantityOf(trade.InstrumentId) + trade.FilledQuantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _cash - trade.TotalAmount - trade.Fee;
        return (new Portfolio(newCash, newPositions.Positions), null);
    }

    private (Portfolio Result, string? Warning) Sell(TradeResult trade)
    {
        if (trade.FilledQuantity <= 0)
            return (this, Messages.QuantityMustBePositive);
        var totalQuantity = QuantityOf(trade.InstrumentId);
        if (totalQuantity < trade.FilledQuantity)
            return (this, Messages.InsufficientQuantityToSell);

        var instrument = _positionSet.GetExistingInstrument(trade.InstrumentId);
        var newQuantity = totalQuantity - trade.FilledQuantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _cash + trade.TotalAmount - trade.Fee;
        return (new Portfolio(newCash, newPositions.Positions), null);
    }
}
