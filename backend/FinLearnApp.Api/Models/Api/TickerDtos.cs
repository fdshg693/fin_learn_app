using System;

namespace FinLearnApp.Api.Models.Api;

public sealed record TickerSummaryDto
{
    public Guid TickerId { get; }
    public string Symbol { get; }
    public string CompanyName { get; }
    public MoneyDto CurrentPrice { get; }

    public TickerSummaryDto(Guid tickerId, string symbol, string companyName, MoneyDto currentPrice)
    {
        TickerId = tickerId;
        Symbol = symbol;
        CompanyName = companyName;
        CurrentPrice = currentPrice;
    }
}

public sealed record TickerDetailDto
{
    public Guid TickerId { get; }
    public string Symbol { get; }
    public string CompanyName { get; }
    public int UnitSize { get; }
    public MoneyDto CurrentPrice { get; }

    public TickerDetailDto(Guid tickerId, string symbol, string companyName, int unitSize, MoneyDto currentPrice)
    {
        TickerId = tickerId;
        Symbol = symbol;
        CompanyName = companyName;
        UnitSize = unitSize;
        CurrentPrice = currentPrice;
    }
}
