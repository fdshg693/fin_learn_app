namespace FinLearn.Core;

/// <summary>
/// 取引結果（プレイヤー向け約定結果、OrderBookの内部状態を含まない）
/// </summary>
public sealed record TradeResult(
    int InstrumentId,
    OrderSide Side,
    int FilledQuantity,
    int TotalAmount,
    int Fee);
