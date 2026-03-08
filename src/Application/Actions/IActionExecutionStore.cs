using FinLearnApp.Domain.Entities;
using FinLearnApp.Domain.ValueObjects;

namespace FinLearnApp.Application.Actions;

public interface IActionExecutionStore
{
    Portfolio? FindPortfolioByInvestor(InvestorId investorId);
    Ticker? FindTicker(TickerId tickerId);
    int GetCurrentTurn(InvestorId investorId);
    int AdvanceTurn(InvestorId investorId);
}
