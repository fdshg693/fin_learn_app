using System;
using System.Collections.Generic;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Ticker
{
    private readonly List<PriceRecord> _priceHistory = new();
    private const int MaxHistorySize = 100;

    public TickerId Id { get; }
    public CompanyId CompanyId { get; }
    public string Symbol { get; }
    public int UnitSize { get; }
    public Money CurrentPrice { get; private set; }
    public IReadOnlyList<PriceRecord> PriceHistory => _priceHistory.AsReadOnly();

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
        _priceHistory.Add(new PriceRecord(0, currentPrice));
    }

    public void UpdatePrice(Money newPrice, int turn)
    {
        CurrentPrice = newPrice;
        _priceHistory.Add(new PriceRecord(turn, newPrice));
        if (_priceHistory.Count > MaxHistorySize)
        {
            _priceHistory.RemoveAt(0);
        }
    }
}
