using FinLearnApp.Api.Data;
using FinLearnApp.Application.Actions;
using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Api.Services;

/// <summary>
/// InMemoryStoreをアプリケーション層の抽象に適合させるアダプタ。
/// </summary>
public sealed class InMemoryActionExecutionStore : IActionExecutionStore
{
    private readonly InMemoryStore _store;

    public InMemoryActionExecutionStore(InMemoryStore store)
    {
        _store = store;
    }

    public Portfolio? FindPortfolioByInvestor(InvestorId investorId)
    {
        return _store.FindPortfolioByInvestor(investorId);
    }

    public Ticker? FindTicker(TickerId tickerId)
    {
        return _store.FindTicker(tickerId);
    }
}
