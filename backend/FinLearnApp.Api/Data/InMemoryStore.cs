using System;
using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.Services;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

public sealed class InMemoryStore
{
    private readonly Dictionary<CompanyId, Company> _companiesById;
    private readonly Dictionary<TickerId, Ticker> _tickersById;
    private readonly Dictionary<InvestorId, int> _turnByInvestor;
    private readonly Random _random;

    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Ticker> Tickers { get; }
    public IReadOnlyList<Investor> Investors { get; }
    public IReadOnlyList<Portfolio> Portfolios { get; }
    public Exchange Exchange { get; }
    public IReadOnlyList<Trade> Trades => Exchange.Trades;

    public InMemoryStore(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Ticker> tickers,
        IReadOnlyList<Investor> investors,
        IReadOnlyList<Portfolio> portfolios,
        IReadOnlyDictionary<InvestorId, int>? turnByInvestor = null,
        Random? random = null)
    {
        Companies = companies;
        Tickers = tickers;
        Investors = investors;
        Portfolios = portfolios;
        Exchange = new Exchange(Money.Jpy(500m));

        _companiesById = companies.ToDictionary(c => c.Id, c => c);
        _tickersById = tickers.ToDictionary(t => t.Id, t => t);
        _turnByInvestor = turnByInvestor is null
            ? investors.ToDictionary(investor => investor.Id, _ => 0)
            : new Dictionary<InvestorId, int>(turnByInvestor);
        _random = random ?? Random.Shared;
    }

    public Company GetCompany(CompanyId id)
    {
        return _companiesById[id];
    }

    public Ticker? FindTicker(TickerId id)
    {
        return _tickersById.TryGetValue(id, out var ticker) ? ticker : null;
    }

    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
    {
        return Portfolios.FirstOrDefault(p => p.InvestorId == investorId);
    }

    public int GetCurrentTurn(InvestorId investorId)
    {
        return _turnByInvestor.TryGetValue(investorId, out var turn) ? turn : 0;
    }

    public int AdvanceTurn(InvestorId investorId)
    {
        var currentTurn = GetCurrentTurn(investorId);
        var nextTurn = currentTurn + 1;
        _turnByInvestor[investorId] = nextTurn;

        TurnDomainService.AdvanceTurn(Exchange, Tickers, _random, nextTurn);

        return nextTurn;
    }

    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
    {
        var marketPrice = GetTickerOrThrow(tickerId).CurrentPrice;
        return Exchange.ExecuteBuyNow(tickerId, quantity, availableCash, marketPrice);
    }

    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity)
    {
        var marketPrice = GetTickerOrThrow(tickerId).CurrentPrice;
        return Exchange.ExecuteSellNow(tickerId, quantity, marketPrice);
    }

    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
    {
        return Exchange.ExecuteBuyLimit(tickerId, quantity, limitPrice, availableCash);
    }

    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
    {
        return Exchange.ExecuteSellLimit(tickerId, quantity, limitPrice);
    }

    private Ticker GetTickerOrThrow(TickerId tickerId)
    {
        return FindTicker(tickerId)
            ?? throw new InvalidOperationException($"Ticker not found: {tickerId.Value}");
    }
}