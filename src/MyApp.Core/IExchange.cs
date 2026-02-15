namespace MyApp.Core;

/// <summary>
/// 取引所（銘柄の現在価格を取得する）
/// </summary>
public interface IExchange
{
    int PriceOf(int instrumentId);
    int Fee { get; }
}
