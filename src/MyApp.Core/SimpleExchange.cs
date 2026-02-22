namespace MyApp.Core;

/// <summary>
/// 簡易取引所（価格辞書と手数料から構成）
/// </summary>
public sealed class SimpleExchange : IExchange
{
    private readonly IReadOnlyDictionary<int, int> _prices;

    public SimpleExchange(IReadOnlyDictionary<int, int> prices, int fee = 0)
    {
        _prices = prices;
        Fee = fee;
    }

    public bool TryGetPrice(int instrumentId, out int price)
    {
        if (!_prices.TryGetValue(instrumentId, out price))
        {
            price = 0;
            return false;
        }
        if (price <= 0)
        {
            price = 0;
            return false;
        }
        return true;
    }

    public int Fee { get; }
}
