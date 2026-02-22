namespace FinLearn.Core;

/// <summary>
/// 取引所（銘柄の現在価格を取得する）
/// </summary>
public interface IExchange
{
    bool TryGetPrice(int instrumentId, out int price);
    int Fee { get; }
}
