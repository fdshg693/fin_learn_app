using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

public sealed class InMemoryStore
{
    private readonly Dictionary<CompanyId, Company> _companiesById;
    private readonly Dictionary<TickerId, Ticker> _tickersById;
    private readonly Dictionary<InvestorId, int> _turnByInvestor;

    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Ticker> Tickers { get; }
    public IReadOnlyList<Investor> Investors { get; }
    public IReadOnlyList<Portfolio> Portfolios { get; }

    public InMemoryStore(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Ticker> tickers,
        IReadOnlyList<Investor> investors,
        IReadOnlyList<Portfolio> portfolios,
        IReadOnlyDictionary<InvestorId, int>? turnByInvestor = null)
    {
        Companies = companies;
        Tickers = tickers;
        Investors = investors;
        Portfolios = portfolios;

        _companiesById = companies.ToDictionary(c => c.Id, c => c);
        _tickersById = tickers.ToDictionary(t => t.Id, t => t);
        _turnByInvestor = turnByInvestor is null
            ? investors.ToDictionary(investor => investor.Id, _ => 0)
            : new Dictionary<InvestorId, int>(turnByInvestor);
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
        return nextTurn;
    }
}
