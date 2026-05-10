namespace FinLearn.Core;

/// <summary>
/// 複数ポジションの集合 + 現金。
/// 指値発注時は資源を <c>available → reserved</c> に移し、約定で確定、失効で解放する
/// （実取引所の予約モデル）。これにより resting 注文の約定 settlement は失敗しない。
/// </summary>
public sealed class Portfolio
{
    private readonly int _availableCash;
    private readonly int _reservedCash;
    private readonly PositionSet _positionSet;          // 全保有（available + reserved 合算）
    private readonly PositionSet _reservedPositions;    // 売り指値で予約中
    private readonly bool _isInfinite;

    public Portfolio(int cash, IEnumerable<Position> positions)
        : this(cash, reservedCash: 0, positions, reservedPositions: Array.Empty<Position>(), isInfinite: false)
    {
    }

    private Portfolio(int availableCash, int reservedCash, IEnumerable<Position> positions,
        IEnumerable<Position> reservedPositions, bool isInfinite)
    {
        _availableCash = availableCash;
        _reservedCash = reservedCash;
        _positionSet = new PositionSet(positions);
        _reservedPositions = new PositionSet(reservedPositions);
        _isInfinite = isInfinite;
    }

    /// <summary>
    /// コンピュータートレーダー用の「∞」ポートフォリオ。予約系・約定系すべて no-op。
    /// </summary>
    public static Portfolio CreateInfinite() =>
        new Portfolio(availableCash: int.MaxValue, reservedCash: 0,
            positions: Array.Empty<Position>(), reservedPositions: Array.Empty<Position>(), isInfinite: true);

    public int Cash => _availableCash;
    public int ReservedCash => _reservedCash;
    public bool IsInfinite => _isInfinite;

    public int TotalAmount(IExchange exchange)
    {
        return _availableCash + _reservedCash + _positionSet.Amount(exchange);
    }

    public int QuantityOf(int instrumentId)
    {
        return _positionSet.QuantityOf(instrumentId);
    }

    public int ReservedQuantityOf(int instrumentId)
    {
        return _reservedPositions.QuantityOf(instrumentId);
    }

    public int AvailableQuantityOf(int instrumentId)
    {
        return QuantityOf(instrumentId) - ReservedQuantityOf(instrumentId);
    }

    /// <summary>
    /// 成行注文（板に乗らない fill）専用。指値の settlement は <see cref="SettleReservedBuy"/> /
    /// <see cref="SettleReservedSell"/> を使うこと。
    /// </summary>
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
        if (_availableCash < trade.TotalAmount + trade.Fee)
            return (this, Messages.InsufficientCashToBuy);

        var instrument = _positionSet.GetOrCreateInstrument(trade.InstrumentId);
        var newQuantity = QuantityOf(trade.InstrumentId) + trade.FilledQuantity;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newCash = _availableCash - trade.TotalAmount - trade.Fee;
        return (With(availableCash: newCash, positions: newPositions.Positions), null);
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
        var newCash = _availableCash + trade.TotalAmount - trade.Fee;
        return (With(availableCash: newCash, positions: newPositions.Positions), null);
    }

    /// <summary>
    /// 買い指値の発注時に <c>qty * price + fee</c> を available → reserved に移す。
    /// </summary>
    public (Portfolio Result, string? Warning) ReserveBuy(int instrumentId, int quantity, int price, int fee)
    {
        if (_isInfinite)
            return (this, null);
        if (quantity <= 0 || price <= 0)
            return (this, Messages.QuantityMustBePositive);

        var amount = quantity * price + fee;
        if (_availableCash < amount)
            return (this, Messages.InsufficientCashToBuy);

        return (With(availableCash: _availableCash - amount, reservedCash: _reservedCash + amount), null);
    }

    /// <summary>
    /// 売り指値の発注時に保有数量を available → reserved に移す（保有総数 <see cref="QuantityOf"/> は不変）。
    /// </summary>
    public (Portfolio Result, string? Warning) ReserveSell(int instrumentId, int quantity)
    {
        if (_isInfinite)
            return (this, null);
        if (quantity <= 0)
            return (this, Messages.QuantityMustBePositive);

        if (AvailableQuantityOf(instrumentId) < quantity)
            return (this, Messages.InsufficientQuantityToSell);

        var newReserved = AddReservedPosition(instrumentId, quantity);
        return (With(reservedPositions: newReserved.Positions), null);
    }

    /// <summary>
    /// 買い指値の約定確定。
    /// 約定価格は予約価格（指値）以下のため、差額は available cash に戻る。
    /// </summary>
    /// <param name="feeIfFinal">この約定で order が完全消化される場合の fee（per-order セマンティクス）。
    /// 部分約定なら 0 を渡す。</param>
    public Portfolio SettleReservedBuy(int instrumentId, int filledQty, int actualUnitPrice, int reservedUnitPrice, int feeIfFinal)
    {
        if (_isInfinite)
            return this;
        if (filledQty <= 0)
            return this;

        var consumedReserved = filledQty * reservedUnitPrice + feeIfFinal;
        var actualCost = filledQty * actualUnitPrice + feeIfFinal;
        var refund = consumedReserved - actualCost;

        var instrument = _positionSet.GetOrCreateInstrument(instrumentId);
        var newQuantity = QuantityOf(instrumentId) + filledQty;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);

        return With(
            availableCash: _availableCash + refund,
            reservedCash: _reservedCash - consumedReserved,
            positions: newPositions.Positions);
    }

    /// <summary>
    /// 売り指値の約定確定。reserved positions と全保有から filledQty を減算、cash に proceeds を加算。
    /// </summary>
    public Portfolio SettleReservedSell(int instrumentId, int filledQty, int actualUnitPrice, int feeIfFinal)
    {
        if (_isInfinite)
            return this;
        if (filledQty <= 0)
            return this;

        var instrument = _positionSet.GetExistingInstrument(instrumentId);
        var newQuantity = QuantityOf(instrumentId) - filledQty;
        var newPositions = _positionSet.SetQuantity(instrument, newQuantity);
        var newReserved = SubtractReservedPosition(instrumentId, filledQty);

        var proceeds = filledQty * actualUnitPrice - feeIfFinal;

        return With(
            availableCash: _availableCash + proceeds,
            positions: newPositions.Positions,
            reservedPositions: newReserved.Positions);
    }

    /// <summary>
    /// 買い指値の失効・残量解放。残予約 cash を available に戻す。
    /// </summary>
    public Portfolio ReleaseBuyReservation(int remainingQty, int reservedUnitPrice, int remainingFee)
    {
        if (_isInfinite)
            return this;
        if (remainingQty <= 0 && remainingFee <= 0)
            return this;

        var releaseAmount = remainingQty * reservedUnitPrice + remainingFee;
        return With(
            availableCash: _availableCash + releaseAmount,
            reservedCash: _reservedCash - releaseAmount);
    }

    /// <summary>
    /// 売り指値の失効・残量解放。reserved positions のみ減算（全保有 <see cref="QuantityOf"/> は不変）。
    /// </summary>
    public Portfolio ReleaseSellReservation(int instrumentId, int remainingQty)
    {
        if (_isInfinite)
            return this;
        if (remainingQty <= 0)
            return this;

        var newReserved = SubtractReservedPosition(instrumentId, remainingQty);
        return With(reservedPositions: newReserved.Positions);
    }

    private PositionSet AddReservedPosition(int instrumentId, int quantity)
    {
        var instrument = _reservedPositions.GetOrCreateInstrument(instrumentId);
        var newQuantity = _reservedPositions.QuantityOf(instrumentId) + quantity;
        return _reservedPositions.SetQuantity(instrument, newQuantity);
    }

    private PositionSet SubtractReservedPosition(int instrumentId, int quantity)
    {
        var current = _reservedPositions.QuantityOf(instrumentId);
        if (current <= 0)
            return _reservedPositions;          // 予約が無ければ no-op（settlement の冪等性）
        var instrument = _reservedPositions.GetExistingInstrument(instrumentId);
        var newQuantity = Math.Max(0, current - quantity);
        return _reservedPositions.SetQuantity(instrument, newQuantity);
    }

    private Portfolio With(
        int? availableCash = null,
        int? reservedCash = null,
        IEnumerable<Position>? positions = null,
        IEnumerable<Position>? reservedPositions = null)
    {
        return new Portfolio(
            availableCash: availableCash ?? _availableCash,
            reservedCash: reservedCash ?? _reservedCash,
            positions: positions ?? _positionSet.Positions,
            reservedPositions: reservedPositions ?? _reservedPositions.Positions,
            isInfinite: _isInfinite);
    }
}
