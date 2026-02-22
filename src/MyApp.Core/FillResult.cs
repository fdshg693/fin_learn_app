namespace MyApp.Core;

/// <summary>
/// 約定結果（各注文IDごとの約定明細 + 更新後の注文帳）
/// </summary>
public sealed record FillResult(
    IReadOnlyList<OrderFill> Fills,
    OrderBook UpdatedBook)
{
    public OrderFill? GetFill(int orderId) =>
        Fills.FirstOrDefault(f => f.OrderId == orderId);
}
