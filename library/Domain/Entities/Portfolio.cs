using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Domain.Entities;

public sealed class Portfolio
{
    private readonly List<Holding> _holdings = new();

    public PortfolioId Id { get; }
    public InvestorId InvestorId { get; }
    public Money InitialAssets { get; }
    public Money Cash { get; private set; }
    public IReadOnlyCollection<Holding> Holdings => _holdings.AsReadOnly();

    public Portfolio(PortfolioId id, InvestorId investorId, Money initialAssets)
    {
        Id = id;
        InvestorId = investorId;
        InitialAssets = initialAssets;
        Cash = initialAssets;
    }

    public Money CalculateValuation(IReadOnlyDictionary<TickerId, Money> prices)
    {
        var holdingsValue = _holdings
            .Select(holding => holding.MarketValue(prices[holding.TickerId]))
            .Aggregate(Money.Jpy(0m), (sum, value) => sum.Add(value));

        return Cash.Add(holdingsValue);
    }

    public Money CalculateProfitLoss(IReadOnlyDictionary<TickerId, Money> prices)
    {
        return CalculateValuation(prices).Subtract(InitialAssets);
    }

    public void Deposit(Money amount)
    {
        Cash = Cash.Add(amount);
    }

    public void Withdraw(Money amount)
    {
        Cash = Cash.Subtract(amount);
    }

    public void AddOrUpdateHolding(TickerId tickerId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than 0.");
        }

        var holding = _holdings.FirstOrDefault(h => h.TickerId == tickerId);
        if (holding is null)
        {
            _holdings.Add(new Holding(tickerId, quantity));
            return;
        }

        holding.Increase(quantity);
    }

    public void ReduceHolding(TickerId tickerId, int quantity)
    {
        var holding = _holdings.FirstOrDefault(h => h.TickerId == tickerId);
        if (holding is null)
        {
            throw new InvalidOperationException("Holding not found.");
        }

        holding.Decrease(quantity);
        if (holding.Quantity == 0)
        {
            _holdings.Remove(holding);
        }
    }
}
