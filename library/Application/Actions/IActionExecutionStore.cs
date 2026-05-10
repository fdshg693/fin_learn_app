using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

public interface IActionExecutionStore
{
    Portfolio? FindPortfolioByInvestor(InvestorId investorId);
    Ticker? FindTicker(TickerId tickerId);
    int GetCurrentTurn(InvestorId investorId);
    int AdvanceTurn(InvestorId investorId);
    OrderMatchResult ExecuteBuyNow(TickerId tickerId, int quantity, Money availableCash);
    OrderMatchResult ExecuteSellNow(TickerId tickerId, int quantity);
    OrderMatchResult ExecuteBuyLimit(TickerId tickerId, int quantity, Money limitPrice, Money availableCash);
    OrderMatchResult ExecuteSellLimit(TickerId tickerId, int quantity, Money limitPrice);
}
