namespace MyApp.Core;

/// <summary>
/// マッチング結果（取引結果 + 更新後の注文帳、Game内部で使用）
/// </summary>
public sealed record MatchResult(TradeResult Trade, OrderBook UpdatedBook);
