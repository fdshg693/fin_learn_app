using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

/// <summary>
/// 投資家注文を即時マッチングした結果。
/// </summary>
public sealed class OrderMatchResult
{
    public int RequestedQuantity { get; }
    public int ExecutedQuantity { get; }
    public Money TotalAmount { get; }

    public int RemainingQuantity => RequestedQuantity - ExecutedQuantity;

    public OrderMatchResult(int requestedQuantity, int executedQuantity, Money totalAmount)
    {
        RequestedQuantity = requestedQuantity;
        ExecutedQuantity = executedQuantity;
        TotalAmount = totalAmount;
    }
}
