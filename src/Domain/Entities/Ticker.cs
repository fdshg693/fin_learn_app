using System;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Ticker
{
    public TickerId Id { get; }
    public CompanyId CompanyId { get; }
    public string Symbol { get; }
    public int UnitSize { get; }
    public Money CurrentPrice { get; private set; }

    public Ticker(TickerId id, CompanyId companyId, string symbol, int unitSize, Money currentPrice)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (unitSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitSize), "Unit size must be greater than 0.");
        }

        Id = id;
        CompanyId = companyId;
        Symbol = symbol;
        UnitSize = unitSize;
        CurrentPrice = currentPrice;
    }

    public void UpdatePrice(Money newPrice)
    {
        CurrentPrice = newPrice;
    }
}
