namespace MyApp.Tests;

using System.Collections.Generic;
using MyApp.Core;

public sealed class TestExchange : IExchange
{
    private readonly IReadOnlyDictionary<int, int> _prices;

    public TestExchange(IReadOnlyDictionary<int, int> prices)
    {
        _prices = prices;
    }

    public int PriceOf(int instrumentId)
    {
        return _prices[instrumentId];
    }
}
