using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.ValueObjects;
using Microsoft.AspNetCore.Mvc;

namespace FinLearnApp.Api.Controllers;

[ApiController]
[Route("api/portfolios")]
public sealed class PortfoliosController : ControllerBase
{
    private readonly InMemoryStore _store;

    public PortfoliosController(InMemoryStore store)
    {
        _store = store;
    }

    [HttpGet("{investorId:guid}")]
    public ActionResult<PortfolioDto> GetPortfolio(Guid investorId)
    {
        var portfolio = _store.FindPortfolioByInvestor(new InvestorId(investorId));
        if (portfolio is null)
        {
            return NotFound();
        }

        return Ok(ToPortfolioDto(portfolio));
    }

    private PortfolioDto ToPortfolioDto(FinLearnApp.Domain.Entities.Portfolio portfolio)
    {
        var prices = _store.Tickers.ToDictionary(t => t.Id, t => t.CurrentPrice);

        var holdings = portfolio.Holdings
            .Select(holding =>
            {
                var ticker = _store.FindTicker(holding.TickerId);
                var price = prices[holding.TickerId];
                return new HoldingDto(
                    holding.TickerId.Value,
                    ticker?.Symbol ?? string.Empty,
                    holding.Quantity,
                    ToMoneyDto(holding.MarketValue(price)));
            })
            .ToList();

        return new PortfolioDto(
            portfolio.Id.Value,
            portfolio.InvestorId.Value,
            ToMoneyDto(portfolio.Cash),
            ToMoneyDto(portfolio.CalculateValuation(prices)),
            ToMoneyDto(portfolio.CalculateProfitLoss(prices)),
            holdings);
    }

    private static MoneyDto ToMoneyDto(Money money)
        => new(money.Amount, money.Currency.ToString());
}
