using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Api.Data;
using FinLearnApp.Api.Models.Api;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Mappers;

/// <summary>
/// ドメインの <c>Portfolio</c> を API返却用DTOに変換する。
/// </summary>
public sealed class PortfolioMapper
{
    private readonly InMemoryStore _store;

    public PortfolioMapper(InMemoryStore store)
    {
        _store = store;
    }

    public PortfolioDto ToDto(Portfolio portfolio)
    {
        var prices = _store.Tickers.ToDictionary(
            ticker => ticker.Id,
            ticker => ticker.CurrentPrice);

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
    {
        return new MoneyDto(money.Amount, money.Currency.ToString());
    }
}
