namespace FinLearn.Tests;

using System.Collections.Generic;
using FinLearn.Core;

public sealed class TestExchange : IExchange
{
    private readonly IReadOnlyDictionary<int, int> _prices;

    public TestExchange(IReadOnlyDictionary<int, int> prices, int fee = 0)
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
