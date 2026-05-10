namespace FinLearnApp.Domain.ValueObjects;

/// <summary>
/// 投資家注文を即時マッチングした結果。
/// RequestedQuantity 株を要求し、ExecutedQuantity 株が約定したことを表す。
/// </summary>
public sealed class OrderMatchResult
{
    /// <summary>要求した株数。</summary>
    public int RequestedQuantity { get; }

    /// <summary>実際に約定した株数。</summary>
    public int ExecutedQuantity { get; }

    /// <summary>約定した総金額（約定しなかった分は含まない）。</summary>
    public Money TotalAmount { get; }

    /// <summary>未約定の株数（RequestedQuantity - ExecutedQuantity）。</summary>
    public int RemainingQuantity => RequestedQuantity - ExecutedQuantity;

    /// <summary>マッチング結果を生成する。</summary>
    /// <param name="requestedQuantity">要求株数。</param>
    /// <param name="executedQuantity">約定株数。</param>
    /// <param name="totalAmount">約定総額。</param>
    public OrderMatchResult(int requestedQuantity, int executedQuantity, Money totalAmount)
    {
        RequestedQuantity = requestedQuantity;
        ExecutedQuantity = executedQuantity;
        TotalAmount = totalAmount;
    }
}
