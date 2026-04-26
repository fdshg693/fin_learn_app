namespace FinLearn.Core;

/// <summary>
/// マッチング結果（取引結果 + 更新後の注文帳 + 全約定明細、Game内部で使用）
/// </summary>
public sealed record MatchResult(
    TradeResult Trade,
    OrderBook UpdatedBook,
    IReadOnlyList<OrderFill> Fills);
