using System;
using System.Collections.Generic;

namespace FinLearnApp.Api.Models.Api;

public sealed record HoldingDto
{
    public Guid TickerId { get; }
    public string Symbol { get; }
    public int Quantity { get; }
    public MoneyDto MarketValue { get; }

    public HoldingDto(Guid tickerId, string symbol, int quantity, MoneyDto marketValue)
    {
        TickerId = tickerId;
        Symbol = symbol;
        Quantity = quantity;
        MarketValue = marketValue;
    }
}

public sealed record PortfolioDto
{
    public Guid PortfolioId { get; }
    public Guid InvestorId { get; }
    public MoneyDto Cash { get; }
    public MoneyDto Valuation { get; }
    public MoneyDto ProfitLoss { get; }
    public IReadOnlyList<HoldingDto> Holdings { get; }

    public PortfolioDto(
        Guid portfolioId,
        Guid investorId,
        MoneyDto cash,
        MoneyDto valuation,
        MoneyDto profitLoss,
        IReadOnlyList<HoldingDto> holdings)
    {
        PortfolioId = portfolioId;
        InvestorId = investorId;
        Cash = cash;
        Valuation = valuation;
        ProfitLoss = profitLoss;
        Holdings = holdings;
    }
}
