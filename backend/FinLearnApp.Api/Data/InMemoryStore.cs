using System.Collections.Generic;
using System.Linq;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Data;

public sealed class InMemoryStore
{
    private readonly Dictionary<CompanyId, Company> _companiesById;
    private readonly Dictionary<TickerId, Ticker> _tickersById;

    public IReadOnlyList<Company> Companies { get; }
    public IReadOnlyList<Ticker> Tickers { get; }
    public IReadOnlyList<Investor> Investors { get; }
    public IReadOnlyList<Portfolio> Portfolios { get; }

    public InMemoryStore(
        IReadOnlyList<Company> companies,
        IReadOnlyList<Ticker> tickers,
        IReadOnlyList<Investor> investors,
        IReadOnlyList<Portfolio> portfolios)
    {
        Companies = companies;
        Tickers = tickers;
        Investors = investors;
        Portfolios = portfolios;

        _companiesById = companies.ToDictionary(c => c.Id, c => c);
        _tickersById = tickers.ToDictionary(t => t.Id, t => t);
    }

    public Company GetCompany(CompanyId id) => _companiesById[id];

    public Ticker? FindTicker(TickerId id)
        => _tickersById.TryGetValue(id, out var ticker) ? ticker : null;

    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
        => Portfolios.FirstOrDefault(p => p.InvestorId == investorId);
}
