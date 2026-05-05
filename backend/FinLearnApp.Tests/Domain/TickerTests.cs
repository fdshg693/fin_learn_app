using System;
using System.Collections.Generic;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Tests.Domain;

public class TickerTests
{
    private static readonly TickerId TestTickerId = new(Guid.Parse("aaaa0000-0000-0000-0000-000000000001"));
    private static readonly CompanyId TestCompanyId = new(Guid.Parse("bbbb0000-0000-0000-0000-000000000001"));

    private static Ticker CreateTicker(decimal price = 1_000m)
        => new(TestTickerId, TestCompanyId, "TEST", 1, Money.Jpy(price));

    [Fact]
    public void Ticker_InitialPriceHistory_ContainsTurnZeroWithInitialPrice()
    {
        var ticker = CreateTicker(1_000m);

        Assert.Single(ticker.PriceHistory);
        Assert.Equal(0, ticker.PriceHistory[0].Turn);
        Assert.Equal(1_000m, ticker.PriceHistory[0].Price.Amount);
    }

    [Fact]
    public void Ticker_UpdatePrice_AppendsToHistory()
    {
        var ticker = CreateTicker(1_000m);

        ticker.UpdatePrice(Money.Jpy(1_050m), turn: 1);
        ticker.UpdatePrice(Money.Jpy(1_100m), turn: 2);

        Assert.Equal(3, ticker.PriceHistory.Count);
        Assert.Equal(1, ticker.PriceHistory[1].Turn);
        Assert.Equal(1_050m, ticker.PriceHistory[1].Price.Amount);
        Assert.Equal(2, ticker.PriceHistory[2].Turn);
        Assert.Equal(1_100m, ticker.PriceHistory[2].Price.Amount);
    }

    [Fact]
    public void Ticker_UpdatePrice_ExceedsMaxHistory_RemovesOldest()
    {
        var ticker = CreateTicker(1_000m);

        for (int i = 1; i <= 100; i++)
        {
            ticker.UpdatePrice(Money.Jpy(1_000m + i), turn: i);
        }

        Assert.Equal(100, ticker.PriceHistory.Count);
        Assert.Equal(1, ticker.PriceHistory[0].Turn);
    }

    [Fact]
    public void Ticker_PriceHistory_ReturnsInChronologicalOrder()
    {
        var ticker = CreateTicker(1_000m);
        ticker.UpdatePrice(Money.Jpy(1_010m), turn: 1);
        ticker.UpdatePrice(Money.Jpy(1_020m), turn: 2);

        Assert.Equal(0, ticker.PriceHistory[0].Turn);
        Assert.Equal(1, ticker.PriceHistory[1].Turn);
        Assert.Equal(2, ticker.PriceHistory[2].Turn);
    }
}
