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

    public int GetCurrentTurn(InvestorId investorId)
    {
        return _store.GetCurrentTurn(investorId);
    }

    public int AdvanceTurn(InvestorId investorId)
    {
        return _store.AdvanceTurn(investorId);
    }

    public OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash)
    {
        return _store.ExecuteBuyNow(tickerId, quantity, availableCash);
    }

    public OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity)
    {
        return _store.ExecuteSellNow(tickerId, quantity);
    }

    public OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash)
    {
        return _store.ExecuteBuyLimit(tickerId, quantity, limitPrice, availableCash);
    }

    public OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice)
    {
        return _store.ExecuteSellLimit(tickerId, quantity, limitPrice);
    }
}
