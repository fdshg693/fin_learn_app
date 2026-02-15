namespace MyApp.Tests;

using System.Collections.Generic;
using MyApp.Core;

public sealed class TestExchange : IExchange
{
    private readonly IReadOnlyDictionary<int, int> _prices;

    public TestExchange(IReadOnlyDictionary<int, int> prices, int fee = 0)
    {
        _prices = prices;
        Fee = fee;
    }

    public int PriceOf(int instrumentId)
    {
        return _prices[instrumentId];
    }

    public int Fee { get; }
}
