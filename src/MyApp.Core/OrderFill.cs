namespace MyApp.Core;

/// <summary>
/// 注文約定明細（各注文IDに紐づく約定結果）
/// </summary>
public sealed record OrderFill(int OrderId, int FilledQuantity, int TotalAmount);
