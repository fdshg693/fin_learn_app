namespace MyApp.Core;

/// <summary>
/// 約定結果（約定数量・合計金額・更新後の注文帳）
/// </summary>
public sealed record FillResult(int FilledQuantity, int TotalAmount, OrderBook UpdatedBook);
